using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Text;
using static Sokol.SLog;
using Sokol;


public static partial class GltfViewer
{
    static StringBuilder dumpOutput = new StringBuilder();
    
    static void DumpModelInfoToFile()
    {
        if (state.model == null)
        {
            Info("No model loaded");
            return;
        }

        dumpOutput.Clear();
        
        dumpOutput.AppendLine("=== MODEL INFO DUMP ===");
        dumpOutput.AppendLine($"File: {filename}");
        dumpOutput.AppendLine($"Meshes: {state.model.Meshes.Count}");
        dumpOutput.AppendLine($"Nodes: {state.model.Nodes.Count}");
        dumpOutput.AppendLine($"Bones: {state.model.BoneCounter}");
        dumpOutput.AppendLine();
        
        dumpOutput.AppendLine("--- Scene Bounds ---");
        var sceneBounds = CalculateSceneBounds();
        dumpOutput.AppendLine($"Min: ({sceneBounds.min.X:F2}, {sceneBounds.min.Y:F2}, {sceneBounds.min.Z:F2})");
        dumpOutput.AppendLine($"Max: ({sceneBounds.max.X:F2}, {sceneBounds.max.Y:F2}, {sceneBounds.max.Z:F2})");
        Vector3 size = sceneBounds.max - sceneBounds.min;
        dumpOutput.AppendLine($"Size: ({size.X:F2}, {size.Y:F2}, {size.Z:F2})");
        dumpOutput.AppendLine();
        
        dumpOutput.AppendLine("--- Node Hierarchy ---");
        // Iterate through all nodes and dump only root nodes (the recursive function will handle children)
        for (int i = 0; i < state.model.Nodes.Count; i++)
        {
            var node = state.model.Nodes[i];
            if (node.Parent == null)  // Only root nodes
            {
                DumpNodeHierarchy(node, i, 0);
            }
        }
        
        dumpOutput.AppendLine();
        dumpOutput.AppendLine("=== END MODEL INFO DUMP ===");
        
        // Save to file
        try
        {
            string outputFileName = Path.GetFileNameWithoutExtension(filename) + "_dump.txt";
            string outputPath = Path.Combine(Environment.CurrentDirectory, outputFileName);
            File.WriteAllText(outputPath, dumpOutput.ToString());
            Info($"Model info dumped to: {outputPath}");
        }
        catch (Exception ex)
        {
            Info($"Failed to save dump file: {ex.Message}");
        }
    }

    static void DumpNodeHierarchy(SharpGltfNode node, int nodeIndex, int depth)
    {
        string indent = new string(' ', depth * 4);
        string treeChar = depth > 0 ? "└── " : "";
        
        // Node name only (no components on same line)
        dumpOutput.AppendLine($"{indent}{treeChar}[{nodeIndex}] {node.NodeName}");
        
        // Properties section - visually distinct from children with │
        string propIndent = indent + (depth > 0 ? "    " : "");
        
        // Show both local and world transforms
        var localTransform = node.GetLocalTransform();
        var worldTransform = node.WorldTransform;
        
        Vector3 localPos, localScale, worldPos, worldScale;
        Quaternion localRot, worldRot;
        Matrix4x4.Decompose(localTransform, out localScale, out localRot, out localPos);
        Matrix4x4.Decompose(worldTransform, out worldScale, out worldRot, out worldPos);
        
        Vector3 localEuler = QuaternionToEuler(localRot);
        Vector3 worldEuler = QuaternionToEuler(worldRot);
        
        dumpOutput.AppendLine($"{propIndent}│ Position: ({localPos.X:F2}, {localPos.Y:F2}, {localPos.Z:F2}) | World: ({worldPos.X:F2}, {worldPos.Y:F2}, {worldPos.Z:F2})");
        dumpOutput.AppendLine($"{propIndent}│ Rotation: ({localEuler.X:F0}°, {localEuler.Y:F0}°, {localEuler.Z:F0}°) | World: ({worldEuler.X:F0}°, {worldEuler.Y:F0}°, {worldEuler.Z:F0}°)");
        dumpOutput.AppendLine($"{propIndent}│ Scale: ({localScale.X:F2}, {localScale.Y:F2}, {localScale.Z:F2}) | World: ({worldScale.X:F2}, {worldScale.Y:F2}, {worldScale.Z:F2})");
        
        // Component details section - all components on separate lines
        
        // Mesh component
        if (node.MeshIndex >= 0)
        {
            if (node.IsSkinned)
                dumpOutput.AppendLine($"{propIndent}│ ├─ Skinned Mesh");
            else
                dumpOutput.AppendLine($"{propIndent}│ ├─ Mesh");
        }
        
        // Camera component
        if (node.Camera != null)
        {
            dumpOutput.AppendLine($"{propIndent}│ ├─ Camera");
        }
        
        // Light component
        if (node.PunctualLight != null)
        {
            string lightType = node.PunctualLight.LightType.ToString();
            dumpOutput.AppendLine($"{propIndent}│ ├─ Light: {lightType}");
        }
        
        // Physics details
        if (node.PhysicsBody != null)
        {
            var physicsBody = node.PhysicsBody;
            
            if (physicsBody.Motion != null)
            {
                string motionType = physicsBody.Motion.Type?.ToUpper() ?? "DYNAMIC";
                dumpOutput.Append($"{propIndent}│ ├─ Rigidbody: {motionType}");
                if (physicsBody.Motion.Mass.HasValue)
                    dumpOutput.Append($", Mass: {physicsBody.Motion.Mass.Value:F2}");
                dumpOutput.AppendLine();
            }
            
            if (physicsBody.Collider != null && node.PhysicsShape != null)
            {
                var shape = node.PhysicsShape;
                dumpOutput.Append($"{propIndent}│ ├─ Collider: {shape.Type}");
                
                if (shape.Box != null && shape.Box.Size != null && shape.Box.Size.Length == 3)
                {
                    dumpOutput.Append($" Size: ({shape.Box.Size[0]:F2}, {shape.Box.Size[1]:F2}, {shape.Box.Size[2]:F2})");
                }
                else if (shape.Sphere != null)
                {
                    dumpOutput.Append($" Radius: {shape.Sphere.Radius:F2}");
                }
                else if (shape.Capsule != null)
                {
                    dumpOutput.Append($" Height: {shape.Capsule.Height:F2}, Radius: {shape.Capsule.RadiusBottom:F2}");
                }
                else if (shape.Cylinder != null)
                {
                    dumpOutput.Append($" Height: {shape.Cylinder.Height:F2}, Radius: {shape.Cylinder.RadiusBottom:F2}");
                }
                
                dumpOutput.AppendLine();
            }
            
            if (physicsBody.Trigger != null)
            {
                dumpOutput.AppendLine($"{propIndent}│ ├─ Trigger");
            }
        }
        
        // Recurse to children
        if (node.Children.Count > 0)
        {
            dumpOutput.AppendLine($"{propIndent}│");  // Blank line with vertical separator before children start
        }
        
        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            // Find the index of this child in the model's Nodes list
            int childIndex = state.model!.Nodes.IndexOf(child);
            if (childIndex >= 0)
            {
                DumpNodeHierarchy(child, childIndex, depth + 1);
                
                // Add vertical line separator between siblings
                if (i < node.Children.Count - 1)
                {
                    dumpOutput.AppendLine($"{indent}    │");
                    dumpOutput.AppendLine($"{indent}    │");
                }
            }
        }
    }
}
