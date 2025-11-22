// Global using aliases to resolve namespace conflicts
// Some BepuPhysics files already have this alias locally, which will generate CS1537 warnings
// We suppress this warning globally since the aliases resolve to the same type
#pragma warning disable CS1537 // The using alias appeared previously in this namespace
global using Task = BepuUtilities.TaskScheduling.Task;
#pragma warning restore CS1537
