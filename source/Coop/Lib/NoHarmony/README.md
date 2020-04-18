NoHarmony is a submodloader for mount and blade : Bannerlord, it allows you to load and replace game models and behaviors simply with a lot more control then the game normally allows it.

To use itsimply derive your own submodule from NoHarmony and fill NoHarmonyLoad() and NoHarmonyInit() to have it handle most of the loading operations.

An example is given that shows you how to use it to replace three game models in game, simply download the VS project and replace <gamedir> in ExampleSubModule.csproj to replace it with the path your game have been installed.