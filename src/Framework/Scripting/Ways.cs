// Ways.cs — Phase H gap E4 (GameEditorProjects/Caperlot/docs/DESIGN.md §7, "Numbers by tier"): how much of the scene's
// traffic and how many of its walking people are out, for a game to set by its quality level.

using GameEditor.Framework.Scene;

namespace GameEditor.Framework.Scripting
{
    public static class Ways
    {
        /// <summary>
        /// Keeps out the first <paramref name="cars"/> of the scene's traffic cars and the first <paramref name="people"/> of
        /// its walking people (-1: all); the others wait hidden, and come out - out of the camera's view - when the numbers
        /// grow. Back to all when the game stops.
        /// </summary>
        public static void Keep(int cars, int people)
        {
            WaysSystem.KeepCars = cars;
            WaysSystem.KeepPeople = people;
        }

        /// <summary>How many traffic cars are out now.</summary>
        public static int CarsOut => WaysSystem.CarsOut;

        /// <summary>How many walking people are out now.</summary>
        public static int PeopleOut => WaysSystem.PeopleOut;
    }
}
