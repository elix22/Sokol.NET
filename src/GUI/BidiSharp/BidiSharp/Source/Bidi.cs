/*
    BidiSharp: Bidirectional algorithm C# implementation

    Copyright (c) 2019 Fayyad Sufyan
    
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

using System;
using System.Text;
using System.Collections.Generic;


namespace BidiSharp
{
    public static class Bidi
    {
        // Max explicity depth (embedding level)
        private const int MAX_DEPTH = 125;

        private struct DirectionalStatus
        {
            internal byte    paragraphEmbeddingLevel;        // 0 >= value <= MAX_DEPTH
            internal byte    directionalOverrideStatus;      // N, R or L
            internal bool    directionalIsolateStatus;
        }

        private class IsolatingRunSequence
        {
            public byte         level;
            public BidiClass    sos;
            public BidiClass    eos;
            public int          length;
            public int[]        indexes;
            public byte[]       types;
            public byte[]       resolvedLevels;

            public IsolatingRunSequence(byte paragraphEmbeddingLevel, List<int> runIndexList, byte[] types, byte[] levels)
            {
                ComputeIsolatingRunSequence(this, paragraphEmbeddingLevel, runIndexList, types, levels);
            }
        }

        // Entry point for algorithm to return at final correct display order
        public static string LogicalToVisual(string input, int[] lineBreaks = null)
        {
            // Optimization:
            // Only continue if an RTL character is present
            
            int     inputLength               = input.Length;
            byte[]  typesList                 = new byte[input.Length];
            byte[]  levelsList                = new byte[input.Length];
            int[]   matchingPDI;
            int[]   matchingIsolateInitiator;

            // Analyze text bidi_class types
            ClassifyCharacters(input, ref typesList);

            // Determine Matching PDI
            GetMatchingPDI(typesList, out matchingPDI, out matchingIsolateInitiator);

            // 3.3.1 Determine paragraph embedding level
            byte baseLevel = GetParagraphEmbeddingLevel(typesList, matchingPDI);

            // Initialize levelsList to paragraph embedding level
            SetLevels(ref levelsList, baseLevel);

            // 3.3.2 (X1-X8) Determine explicit embedding levels and directions
            GetExplicitEmbeddingLevels(baseLevel, typesList, ref levelsList, matchingPDI);

            /*
            ** Isolating run sequences
            ** 3.3.3,  3.3.4,  3.3.5,  3.3.6
            ** X9,X10  W1-W7   N0-N2   I1-I2
            */
            
            // X9 Remove all RLE, LRE, RLO, LRO, PDF and BN characters
            // Instead of removing, assign the embedding level to each formatting 
            // character and turn it (type or level?) to BN.
            // The goal in marking a formatting or control character as BN is that it 
            // has no effect on the rest of the algorithm (ZWJ and ZWNJ are exceptions).
            RemoveX9Characters(ref typesList);

            // X10 steps
            // .1 Compute isolating run sequences according to BD13. Apply next rules to each sequence
            var levelRuns = GetLevelRuns(levelsList);
            int nRuns = levelRuns.Count;

            // Determine each character belongs to what run
            int[] runCharsArray = GetRunForCharacter(levelRuns, inputLength);

            var sequences = GetIsolatingRunSequences(baseLevel, typesList, levelsList, levelRuns, matchingIsolateInitiator,
                                                     matchingPDI, runCharsArray);

            foreach (var sequence in sequences)
            {
                // Rules W1-W7
                sequence.ResolveWeaks();

                // Rules N0-N2
                sequence.ResolveNeutrals();

                // Rules I1-I2
                sequence.ResolveImplicit();

                sequence.ApplyTypesAndLevels(ref typesList, ref levelsList);
            }

            // Rules L1-L2
            var lines = lineBreaks == null ? new int[] { typesList.Length } : lineBreaks;
            int[] newIndexes = GetReorderedIndexes(baseLevel, typesList, levelsList, lines);

            // Return new text from ordered levels
            var finalStr = GetOrderedString(input, newIndexes);

            return finalStr;
        }

        // 3.2 Determine Bidi_class of each input character
        private static void ClassifyCharacters(string text, ref byte[] typesList)
        {
            typesList = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                int chIndex     = Convert.ToInt32(text[i]);
                typesList[i]    = Bidi_Types.BidiCharTypes[chIndex];
            }
        }

        // Rules P2, P3 Determine paragraph embedding level given types array and optional 
        // start and end index to treat types as a scoped paragraph (useful for rule X5c)
        private static byte GetParagraphEmbeddingLevel(byte[] types, int[] matchingPDI, int si = -1, int ei = -1)
        {
            int start   = si != -1 ? si : 0;
            int end     = ei != -1 ? ei : types.Length;

            // Find first L, AL or R character
            for (int i = start; i < end; i++)
            {
                var cct = (BidiClass)types[i];
                if(cct == BidiClass.L  || 
                   cct == BidiClass.AL || 
                   cct == BidiClass.R)
                {
                    if(cct == BidiClass.L) return 0;
                    else return 1;
                }
                else if(cct == BidiClass.LRI || 
                        cct == BidiClass.RLI || 
                        cct == BidiClass.FSI)
                {
                    // Skip characters between isolate initiator and matching PDI (if found)
                    i = matchingPDI[i];
                }
            }

            return 0;   // default, no strong character type found
        }

        // 3.3.2 Determine Explicit Embedding Levels and directions
        private static void GetExplicitEmbeddingLevels(byte level, byte[] types, ref byte[] levels, int[] matchingPDI)
        {
            // X1.
            // Directional Status Stack and entry
            Stack<DirectionalStatus> dirStatusStack = new Stack<DirectionalStatus>(MAX_DEPTH + 2);
            DirectionalStatus dirEntry = new DirectionalStatus
            {
                paragraphEmbeddingLevel = level,
                directionalOverrideStatus = (int)BidiClass.ON,
                directionalIsolateStatus = false
            };
            dirStatusStack.Push(dirEntry);
            
            int overflowIsolateCount    = 0;
            int overflowEmbeddingCount  = 0;
            int validIsolateCount       = 0;

            // X2-X8
            for (int i = 0; i < types.Length; i++)
            {
                BidiClass cct = (BidiClass)types[i];
                switch (cct)
                {
                    case BidiClass.RLE:
                    case BidiClass.RLO:
                    case BidiClass.LRE:
                    case BidiClass.LRO:
                    case BidiClass.LRI:
                    case BidiClass.RLI:
                    case BidiClass.FSI:
                    {
                        byte newLevel;      // New calculated embedding level

                        bool isIsolate = (cct == BidiClass.RLI || cct == BidiClass.LRI);

                        // X5a, X5b .1 isolate embedding level
                        if(isIsolate)
                        {
                            levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                        }

                        // X5c. Get embedding level of characters between FSI and its matching PDI
                        // FSI = RLI if embedding level is 1, otherwise LRI

                        if(cct == BidiClass.FSI)
                        {
                            byte el = GetParagraphEmbeddingLevel(types, matchingPDI, i + 1, matchingPDI[i]);
                            cct = el == 1 ? BidiClass.RLI : BidiClass.LRI;
                        }

                        // 1 (RLE RLO RLI, LRE LRO LRI) Compute least odd/even embedding level greater than embedding level
                        //  of last entry on directional status stack
                        if(cct == BidiClass.RLE || cct == BidiClass.RLO || cct == BidiClass.RLI)
                        {
                            newLevel = (byte)LeastGreaterOdd(dirStatusStack.Peek().paragraphEmbeddingLevel);
                        }
                        else
                        {
                            newLevel = (byte)LeastGreaterEven(dirStatusStack.Peek().paragraphEmbeddingLevel);
                        }

                        // 2 New level would be valid(level <= max_depth) and overflow isolate count and
                        // overflow embedding count are both zero => this RLE is valid, increment isolate counter.
                        if(newLevel <= MAX_DEPTH &&  overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                        {
                            // X5b .3
                            if(isIsolate)
                            {
                                validIsolateCount++;
                            }

                            // Push new entry to stack
                            byte dos = cct == BidiClass.RLO ? (byte)BidiClass.R  // RLO = R directional override status
                                    : cct == BidiClass.LRO ? (byte)BidiClass.L   // LRO = L directional override status
                                    : (byte)BidiClass.ON;                        // All rest are neutrals
                            dirStatusStack.Push(new DirectionalStatus()
                            {
                                paragraphEmbeddingLevel = newLevel,
                                directionalOverrideStatus = dos,
                                directionalIsolateStatus = isIsolate
                            });
                        }
                        // 3 Otherwise, this is an overflow RLE. If the overflow isolate count is zero, 
                        // increment the overflow embedding count by one. Leave all other variables unchanged.
                        else
                        {
                            if(overflowIsolateCount == 0)
                            {
                                overflowEmbeddingCount++;
                            }
                        }
                    }
                    break;

                    // X6a Terminating Isolates
                    case BidiClass.PDI:
                    {
                        if (overflowIsolateCount > 0)   // This PDI matches an overflow isolate initiator
                        {
                            overflowIsolateCount--;
                        }
                        else if (validIsolateCount == 0)
                        {
                            // No matching isolator (valid or overflow), do nothing
                        }
                        else // This PDI matches a valid isolate initiator
                        {
                            overflowEmbeddingCount = 0;

                            while (dirStatusStack.Peek().directionalIsolateStatus == false)
                            {
                                dirStatusStack.Pop();
                            }

                            dirStatusStack.Pop();
                            validIsolateCount--;
                        }

                        levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                    }
                    break;

                    // X7
                    case BidiClass.PDF:
                    {
                        if(overflowIsolateCount > 0) // X7 .1
                        {
                            // Do nothing
                        }
                        else if(overflowEmbeddingCount > 0) // X7 .2
                        {
                            overflowEmbeddingCount--;
                        }
                        else if(!dirStatusStack.Peek().directionalIsolateStatus && dirStatusStack.Count > 1) // X7 .3
                        {
                            dirStatusStack.Pop();
                        }
                        else
                        {
                            // Do nothing
                        }
                    }
                    break;

                    // X8
                    case BidiClass.B:
                    {
                        // Paragraph separators.
                        // Applied at the end of paragraph (last character in array).

                        // 1 Terminate(reset) all directional embeddings, overrides and isolates 
                        overflowEmbeddingCount = 0;
                        overflowIsolateCount = 0;
                        validIsolateCount = 0;
                        dirStatusStack.Clear();     // Also pop off initialization entry

                        // 2 Assign separator character an embedding level equal to paragraph embedding level
                        levels[i] = level;
                    }
                    break;

                    // X6 Non-formatting characters
                    default:
                    {
                        levels[i] = dirStatusStack.Peek().paragraphEmbeddingLevel;
                        if(dirStatusStack.Peek().directionalOverrideStatus != (int)BidiClass.ON) // X6.b (6.2.0 naming)
                        {
                            types[i] = dirStatusStack.Peek().directionalOverrideStatus; // reset type to last element status
                        }
                    }
                    break;
                }
            }
        }

        // 3.3.3 Resolve Weak Types
        private static void ResolveWeaks(this IsolatingRunSequence sequence)
        {
            // W1 NSM
            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];
                var prevType = i == 0 ? sequence.sos : (BidiClass)sequence.types[i - 1];
                if(ct == BidiClass.NSM)
                {
                    // if NSM is at start of sequence resolved to sos type
                    // assign ON if previous is isolate initiator or PDI, otherwise type of previous
                    bool isIsolateOrPDI = prevType == BidiClass.LRI || 
                                          prevType == BidiClass.RLI || 
                                          prevType == BidiClass.FSI || 
                                          prevType == BidiClass.PDI;

                    sequence.types[i] = isIsolateOrPDI ? (byte)BidiClass.ON : (byte)prevType;
                }
            }

            // W2 EN
            // At each EN search in backward until first strong type is found, if AL is found then resolve to AN
            for (int i = 0; i < sequence.length; i++)
            {
                var chType = (BidiClass)sequence.types[i];
                if (chType == BidiClass.EN)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var type = (BidiClass)sequence.types[j];                
                        if (type == BidiClass.R  || type == BidiClass.AL || type == BidiClass.L)
                        {
                            if (type == BidiClass.AL)
                            {
                                sequence.types[i] = (byte)BidiClass.AN;
                                break;
                            }
                        }
                    }
                }
            }

            // W3 AL
            // Resolve all ALs to R
            for (int i = 0; i < sequence.length; i++)
            {
                if ((BidiClass)sequence.types[i] == BidiClass.AL)
                {
                    sequence.types[i] = (byte)BidiClass.R;
                }
            }

            // W4 ES, CS (Number Separators)
            // ES between EN is resolved to EN
            // Single CS between same numbers type is resolve to that number type
            for (int i = 1; i < sequence.length - 1; i++)
            {
                var cct         = (BidiClass)sequence.types[i];
                var prevType    = (BidiClass)sequence.types[i - 1];
                var nextType    = (BidiClass)sequence.types[i + 1];

                if (cct == BidiClass.ES && prevType == BidiClass.EN && nextType == BidiClass.EN) // EN ES EN -> EN EN EN
                {
                    sequence.types[i] = (byte)BidiClass.EN;
                }
                else if (cct == BidiClass.CS && (
                prevType == BidiClass.EN && nextType == BidiClass.EN ||
                prevType == BidiClass.AN && nextType == BidiClass.AN))      // EN CS EN -> EN EN EN, AN CS AN -> AN AN AN
                {
                    sequence.types[i] = (byte)prevType;
                }
            }

            // W5 ET(s) adjacent to EN resolve to EN(s)
            var typesSet = new BidiClass[] { BidiClass.ET };
            for (int i = 0; i < sequence.length; i++)
            {
                if ((BidiClass)sequence.types[i] == BidiClass.ET)
                {
                    int runStart = i;
                    // int runEnd = runStart;
                    // runEnd = Array.FindIndex(sequence.types, runStart, t1 => typesSet.Any(t2 => t2 == (BidiClass)t1));
                    int runEnd = sequence.GetRunLimit(runStart, sequence.length, typesSet);

                    var type = runStart > 0 ? (BidiClass)sequence.types[runStart - 1] : sequence.sos;

                    if (type != BidiClass.EN)
                    {
                        type = runEnd < sequence.length ? (BidiClass)sequence.types[runEnd] : sequence.eos; // End type
                    }

                    if (type == BidiClass.EN)
                    {
                        sequence.SetRunTypes(runStart, runEnd, BidiClass.EN); // Resolve to EN
                    }

                    i = runEnd; // advance to end of sequence
                }
            }

            // W6 Separators and Terminators -> ON
            for (int i = 0; i < sequence.length; i++)
            {
                var t = (BidiClass)sequence.types[i];
                if (t == BidiClass.ET || t == BidiClass.ES || t == BidiClass.CS)
                {
                    sequence.types[i] = (byte)BidiClass.ON;
                }
            }

            // W7 same as W2 but EN -> L
            for (int i = 0; i < sequence.length; i++)
            {
                if((BidiClass)sequence.types[i] == BidiClass.EN)
                {
                    var prevStrong = sequence.sos;  // Default to sos if reached start
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var t = (BidiClass)sequence.types[j];
                        if (t == BidiClass.R || t == BidiClass.L || t == BidiClass.AL)
                        {
                            prevStrong = t;
                            break;
                        }

                        if (prevStrong == BidiClass.L)
                        {
                            sequence.types[i] = (byte)BidiClass.L;
                        }
                    }
                }
            }

        }

        // 3.3.4 Resolve Neutral Types
        // In final results all NIs are resolved to R or L
        private static void ResolveNeutrals(this IsolatingRunSequence sequence)
        {

            // TODO: N0 rule (Paired Brackets algorithm)

            // N1
            // Sequence of NIs will resolve to surrounding "strong" type if text on both sides was of same direction.
            // sos and eos are used at run sequence boundaries. AN and EN will resolve type to R.
            var typesSet = new BidiClass[] { BidiClass.B, BidiClass.S, BidiClass.WS, BidiClass.ON, BidiClass.LRI, BidiClass.RLI, BidiClass.FSI, BidiClass.PDI };
            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];
                bool isNI = ct == BidiClass.B   ||
                            ct == BidiClass.S   ||
                            ct == BidiClass.WS  ||
                            ct == BidiClass.ON  ||
                            ct == BidiClass.LRI ||
                            ct == BidiClass.RLI ||
                            ct == BidiClass.FSI ||
                            ct == BidiClass.PDI;

                if (isNI)
                {
                    BidiClass   leadType  = 0;
                    BidiClass   trailType = 0;
                    int         start     = i;
                    int         runEnd    = sequence.GetRunLimit(start, sequence.length, typesSet);

                    // Start of matching NI
                    if (start == 0) // Start boundary, lead type = sos
                    {
                        leadType = sequence.sos;
                    }
                    else
                    {
                        leadType = (BidiClass)sequence.types[start - 1];
                        if (leadType == BidiClass.AN || leadType == BidiClass.EN)   // Leading AN, EN resolve type to R
                        {
                            leadType = BidiClass.R;
                        }
                    }

                    // End of Matching NI
                    if (runEnd == sequence.length) // End boundary. trail type = eos
                    {
                        trailType = sequence.eos;
                    }
                    else
                    {
                        trailType = (BidiClass)sequence.types[runEnd];
                        if (trailType == BidiClass.AN || trailType == BidiClass.EN)
                        {
                            trailType = BidiClass.R;
                        }
                    }

                    if (leadType == trailType)
                    {
                        sequence.SetRunTypes(start, runEnd, leadType);
                    }
                    else    // N2
                    {
                        // Remaining NIs take current run embedding level
                        var runDirection = GetTypeForLevel(sequence.level);
                        sequence.SetRunTypes(start, runEnd, runDirection);
                    }

                    i = runEnd;
                }
            }
        }

        // 3.3.5 Resolve Implicit Embedding Levels
        private static void ResolveImplicit(this IsolatingRunSequence sequence)
        {
            byte level = sequence.level;

            // Initialize the sequence resolved levels with sequence embedding level
            sequence.resolvedLevels = new byte[sequence.length];
            SetLevels(ref sequence.resolvedLevels, sequence.level);

            for (int i = 0; i < sequence.length; i++)
            {
                var ct = (BidiClass)sequence.types[i];

                // I1
                // Sequence level is even (Left-to-right) then R types go up one level, AN and EN go up two levels
                if (!IsOdd(level))
                {
                    if (ct == BidiClass.R)
                    {
                        sequence.resolvedLevels[i] += 1;
                    }
                    else if(ct == BidiClass.AN || ct == BidiClass.EN)
                    {
                        sequence.resolvedLevels[i] += 2;
                    }
                }
                // N2
                // Sequence level is odd (Right-to-left) then L, AN, EN go up one level
                else
                {
                    if (ct == BidiClass.L || ct == BidiClass.AN || ct == BidiClass.EN)
                    {
                        sequence.resolvedLevels[i] += 1;
                    }
                }
            }
        }

        private static void ApplyTypesAndLevels(this IsolatingRunSequence sequence, ref byte[] typesList, ref byte[] levelsList)
        {
            for (int i = 0; i < sequence.length; i++)
            {
                int idx         = sequence.indexes[i];
                typesList[idx]  = sequence.types[i];
                levelsList[idx] = sequence.resolvedLevels[i];
            }
        }

        // Entry for Rules L1-L2
        // Return the final ordered levels array including the line breaks
        private static int[] GetReorderedIndexes(byte level, byte[] typesList, byte[] levelsList, int[] lineBreaks)
        {
            var levels = GetTextLevels(level, typesList, levelsList, lineBreaks);
            
            var multilineLevels = GetMultiLineReordered(levels, lineBreaks);

            return multilineLevels;
        }

        private static void GetMatchingPDI(byte[] types, out int[] outMatchingPDI, out int[] outMatchingIsolateInitiator)
        {
            int[] matchingPDI = new int[types.Length];
            int[] matchingIsolateInitiator = new int[types.Length];
            
            // Scan for isolate initiator
            for (int i = 0; i < types.Length; i++)
            {
                var cct = (BidiClass)types[i];
                if(cct == BidiClass.LRI || 
                   cct == BidiClass.RLI || 
                   cct == BidiClass.FSI)
                {
                    int  counter         = 1;
                    bool hasMatchingPDI  = false;

                    // Scan the text following isolate initiator till end of paragraph
                    for (int j = i + 1; j < types.Length; j++)
                    {
                        BidiClass nct = (BidiClass)types[j];
                        if(nct == BidiClass.LRI || 
                           nct == BidiClass.RLI || 
                           nct == BidiClass.FSI)        // Increment counter at every isolate initiator
                        {
                            counter++;
                        }
                        else if(nct == BidiClass.PDI)   // Decrement counter at every PDI
                        {
                            counter--;
                            
                            if(counter == 0)            // BD9 bullet 3. Stop when counter is 0
                            {
                                hasMatchingPDI              = true;
                                matchingPDI[i]              = j;      // Matching PDI found
                                matchingIsolateInitiator[j] = i;
                                break;
                            }
                            
                        }
                    }

                    if (!hasMatchingPDI)
                    {
                        matchingPDI[i] = types.Length;
                    }
                }
                else        // Other characters matchingPDI are set to -1
                {
                    matchingPDI[i]              = -1;
                    matchingIsolateInitiator[i] = -1;
                }
            }

            outMatchingPDI              = matchingPDI;
            outMatchingIsolateInitiator = matchingIsolateInitiator;
        }

        private static void RemoveX9Characters(ref byte[] buffer)
        {
            // Todo: ZWJ and ZWNJ characters exception from BN overriding

            // Replace Embedding and override type with BN
            for (int i = 0; i < buffer.Length; i++)
            {
                var ct = (BidiClass)buffer[i];
                if(ct == BidiClass.LRE || ct == BidiClass.RLE ||
                   ct == BidiClass.LRO || ct == BidiClass.RLO)
                {
                    buffer[i] = (byte)BidiClass.BN;
                }
            }
        }

        private static List<List<int>> GetLevelRuns(byte[] levels)
        {
            List<int>       runList         = new List<int>();
            List<List<int>> allRunsList     = new List<List<int>>();

            sbyte currentLevel = -1;
            for (int i = 0; i < levels.Length; i++)
            {
                if(levels[i] != currentLevel)        // New run
                {
                    if(currentLevel >= 0)           // Assign last run
                    {
                        allRunsList.Add(runList);
                        runList.Clear();
                    }

                    currentLevel = (sbyte)levels[i];       // New run level
                }

                runList.Add(i);
            }

            // Append last run
            if (runList.Count > 0)
            {
                allRunsList.Add(runList);
            }

            return allRunsList;
        }

        // Map each character to its belonging run
        private static int[] GetRunForCharacter(List<List<int>> levelRuns, int length)
        {
            int[] runCharsArray = new int[length];
            for (int i = 0; i < levelRuns.Count; i++)
            {
                for (int j = 0; j < levelRuns[i].Count; j++)
                {
                    int chPos = levelRuns[i][j];
                    runCharsArray[chPos] = chPos;
                }
            }

            return runCharsArray;
        }

        private static List<IsolatingRunSequence> GetIsolatingRunSequences(byte pLevel, byte[] types, byte[] levels, 
        List<List<int>> levelRuns, int[] matchingIsolateInitiator, int[] matchingPDI, int[] runCharsArray)
        {
            List<IsolatingRunSequence> allRunSequences = new List<IsolatingRunSequence>(levelRuns.Count);

            foreach (var run in levelRuns)
            {
                List<int> currRunSequence;
                var first = run[0];

                if((BidiClass)types[first] != BidiClass.PDI || matchingIsolateInitiator[first] == -1) // BD13 bullet 2
                {
                    currRunSequence = new List<int>(run);           // initialize a new level run sequence with current run
                    
                    int  lastCh              = currRunSequence[currRunSequence.Count - 1];
                    var  lastType            = (BidiClass)types[lastCh];
                    bool isIsolateInitiator  = lastType == BidiClass.RLI || 
                                               lastType == BidiClass.LRI || 
                                               lastType == BidiClass.FSI;

                    int lastChMatchingPDI = matchingPDI[lastCh];
                    while (isIsolateInitiator && lastChMatchingPDI != types.Length)
                    {
                        var lChRunIndex = runCharsArray[lastChMatchingPDI]; // Get run index for last character that has matchingPDI
                        var newRun = levelRuns[lChRunIndex];
                        currRunSequence.AddRange(newRun);
                    }

                    allRunSequences.Add(new IsolatingRunSequence(pLevel, currRunSequence, types, levels));
                }
            }

            return allRunSequences;
        }

        // X10 bullet 2 Determine start and end of sequence types (R or L) for an isolating run sequence
        // using run sequence indexes
        private static void ComputeIsolatingRunSequence(this IsolatingRunSequence sequence, byte pLevel, List<int> indexList, 
        byte[] typesList, byte[] levels)
        {
            sequence.length = indexList.Count;
            sequence.indexes = indexList.ToArray();                     // Indexes of run in original text
            
            // Character types of run sequence
            sequence.types = new byte[indexList.Count];
            for (int i = 0; i < sequence.length; i++)
            {
                sequence.types[i] = typesList[indexList[i]];
            }

            // sos
            var firstLevel = levels[indexList[0]];      // level of first character
            sequence.level = firstLevel;
            var previous = indexList[0] - 1;
            var prevLevel = previous >= 0 ? levels[previous] : pLevel;
            sequence.sos = GetTypeForLevel(Math.Max(firstLevel, prevLevel));

            // eos
            var lastType     = (BidiClass)sequence.types[sequence.length - 1];
            var last         = indexList[sequence.length - 1];       // last character in the sequence
            var lastLevel    = levels[last];
            var next         = indexList[sequence.length - 1] + 1;   // next character after sequence (in paragraph)
            var nextLevel    = next < typesList.Length && lastType != BidiClass.PDI ? levels[last] : pLevel;
            sequence.eos     = GetTypeForLevel(Math.Max(lastLevel, nextLevel));
        }

        // Override levels list with new level value
        private static void SetLevels(ref byte[] levels, byte newLevel)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = newLevel;
            }
        }

        // Return end index of run consisting of types in typesSet
        // Start from index and check the value, if value not present in set then return index.
        private static int GetRunLimit(this IsolatingRunSequence sequence, int index, int limit, BidiClass[] typesSet)
        {
            loop: for (; index < limit;)
            {
                var type = (BidiClass)sequence.types[index];
                for (int i = 0; i < typesSet.Length; i++)
                {
                    if (type == typesSet[i])
                    {
                        index++;
                        goto loop;
                    }
                }

                // No match in typesSet
                return index;
            }

            return limit;
        }

        // Override types list from start up to (not including) limit to newType
        private static void SetRunTypes(this IsolatingRunSequence sequence, int start, int limit, BidiClass newType)
        {
            for (int i = start; i < limit; i++)
            {
                sequence.types[i] = (byte)newType;
            }
        }

        // Compute least odd level greater than l
        private static int LeastGreaterOdd(int l)
        {
            return IsOdd(l) ? l + 2 : l + 1;
        }
        
        // Compute least even level greater than l
        private static int LeastGreaterEven(int l)
        {
            return !IsOdd(l) ? l + 2: l + 1;
        }

        private static bool IsOdd(int n)
        {
            return (n & 1) != 0;
        }

        // Return L if level is even and R if Odd
        private static BidiClass GetTypeForLevel(byte level)
        {
            return (level & 1) == 0 ? BidiClass.L : BidiClass.R;
        }

        private static byte[] GetTextLevels(byte paragraphEmbeddingLevel, byte[] typesList, byte[] levelsList, int[] lineBreaks)
        {
            byte[] finalLevels = levelsList;

            // Rule L1
            // Level of S and B is changed to the paragraph embedding level.
            // Any sequence of whitespace and/or isolate formatting characters preceding S, B are changed to paragraph level
            for (int i = 0; i < finalLevels.Length; i++)
            {
                var t = (BidiClass)typesList[i];    // Types here are original ones not the output of previous stages

                if (t == BidiClass.S || t == BidiClass.B)
                {
                    finalLevels[i] = paragraphEmbeddingLevel;
                }

                // Search backward for whitespace or isolates (LRI, RLI, FSI, PDI)
                for (int j = i - 1; j >= 0; j--)
                {
                    t = (BidiClass)typesList[j];
                    if (t == BidiClass.LRI ||
                        t == BidiClass.RLI ||
                        t == BidiClass.FSI ||
                        t == BidiClass.FSI)
                    {
                        finalLevels[j] = paragraphEmbeddingLevel;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Search backward for any sequence of whitespace or isolates at ach line breaks (ends)
            int start = 0;
            for (int i = 0; i < lineBreaks.Length; i++)
            {
                int end = lineBreaks[i];    // Line limit (new line start)
                for (int j = end - 1; j >= start; j--)
                {
                    var t = (BidiClass)typesList[j];
                    if (t == BidiClass.LRI ||
                        t == BidiClass.RLI ||
                        t == BidiClass.FSI ||
                        t == BidiClass.FSI)
                    {
                        finalLevels[j] = paragraphEmbeddingLevel;
                    }
                    else
                    {
                        break;
                    }
                }

                start = end; // Reset start to new line start
            }

            return finalLevels;
        }
        
        // Compute correct text indexes using levels array and line breaks positions.
        // Line breaks should be calculated and supplied by the rendering system after shaping and bounds calculations
        private static int[] GetMultiLineReordered(byte[] levels, int[] lineBreaks)
        {
            int[] resultIndexes = new int[levels.Length];

            // Calculate lines levels separately and append them at their final offsets in levels array
            int start = 0;
            for (int i = 0; i < lineBreaks.Length; i++)
            {
                int end = lineBreaks[i];

                var tempLevels = new byte[end - start];  // Line levels
                Array.Copy(levels, start, tempLevels, 0, tempLevels.Length); // Copy line levels to work on it

                var tempReorderedIndexes = ComputeReorderingIndexes(tempLevels); // Rule L2 (reversing)
                for (int j = 0; j < tempReorderedIndexes.Length; j++)
                {
                    resultIndexes[start + j] = tempReorderedIndexes[j] + start;
                }

                start = end; // Next line start
            }

            return resultIndexes;
        }

        // Rule L2
        private static int[] ComputeReorderingIndexes(byte[] levels)
        {
            int lineLength = levels.Length;

            // Initialize line indexes to logical order 0,1,2, etc..
            int[] resultIndexes = new int[lineLength];
            for (int i = 0; i < lineLength; i++)
            {
                resultIndexes[i] = i;
            }

            // Determine highest level on the text
            // scan for highest level and lowest odd level
            byte highestLevel    = 0;
            byte lowestOddLevel  = MAX_DEPTH + 2; // max value for odd levels
            foreach (var level in levels)
            {
                if (level > highestLevel) // highest level
                {
                    highestLevel = level;
                }
                
                // lowest odd level (start from max possible odd levels down to lowest level found)
                if (IsOdd(level) && level < lowestOddLevel)
                {
                    lowestOddLevel = level;
                }
            }

            for (int l = highestLevel; l >= lowestOddLevel; l--)    // Reverse from highest level down to lowest odd level
            {
                for (int i = 0; i < lineLength; i++)
                {
                    if (levels[i] >= l)
                    {
                        int start   = i;
                        int end     = i + 1;

                        while (end < lineLength && levels[end] >= l)    // Text range at this level or above
                        {
                            end++;
                        }

                        for (int j = start, k = end - 1; j < k; j++, k--) // Reverse
                        {
                            int tmp             = resultIndexes[j];
                            resultIndexes[j]    = resultIndexes[k];
                            resultIndexes[k]    = tmp;
                        }

                        i = end; // Skip to end
                    }
                }
            }

            return resultIndexes;
        }

        // Return final correctly reversed string order
        private static string GetOrderedString(string input, int[] newIndexes)
        {
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < newIndexes.Length; i++)
            {
                sb.Append(input[newIndexes[i]]);
            }

            return sb.ToString();
        }
    }
}
