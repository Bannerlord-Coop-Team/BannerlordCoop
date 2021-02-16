# Project Setup
1. Clone the [repository](https://github.com/Bannerlord-Coop-Team/BannerlordCoop) somewhere on your machine.<br/>
2. Submodules will have to be updated using the command `git submodule update --recursive --force`.<br/>
3. Run the `runmefirst.cmd` to dynamically attach your Bannerlord path and resolve references.<br/>
4. If references in projects did not resolved automatically do the following. This can be done in Visual Studio by right-clicking references, going to browse, navigating to your Bannerlord directory through the mb2 shortcut, and selecting all TaleWorld.* .dlls. There are additional .dlls in the Modules folder, being the Native and StoryMode.<br/>
5. Go to project settings for the Coop project and click debug. <br/>
6. Click start external program and browse to your Bannerlord path. You should select the executable for Bannerlord normally located at `bin\Win64_Shipping_Client\Bannerlord.exe` <br/>
7. For the command line arguments enter this `/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_` <br/>
8. Now we need to enter the working directory. NOTE: This will select a folder, not a file. The folder you need to select for this is `bin\Win64_Shipping_Client`. The same path that `Bannerlord.exe` is located. <br/>
9. (Optional) Setup a client instance. <br/>
   1. To run a client instance open the ClientDebug options. Go to debug and do steps 6-8. The difference here is the command line arguments which should be changed to `/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_`.
  
   2. Now open the Solution 'Coop' properties -> Startup Projects. Select multiple projects and make sure Coop and ClientDebug are set to start.
10. Click debug. <br/>
11. Create a new game and save it as "MP". <br/>
12. Click on "host game" at the menu and the game will load the "MP" save file on the host. (and client if that is setup) <br/>
13. If there are any issues contact someone in the discord. <br/>