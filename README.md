> [!NOTE]  
> ## Cove 1.0! 🥳
> As of WebFishing v1.09 cove implements all fetures from the main game making it a parallel experience compared to the actual game!

# Webfishing Cove
Cove is a dedicated server for WebFishing written in C#!

> [!IMPORTANT]  
> Cove is very new, there will be unexpected bugs! 
>
> If you encounter any large issues or bugs please let me know!

Cove is a implementation of the WebFishing network protocall in a CLI meaning it dosent require Godot or anything other than Steamworks!

# How it works
Cove uses none of WebFishing's code, insted it used C# to emulate the same network calls that the offical game would make to host a lobby!

Things like event spawning all had to be written from scratch to allow for the portability of C#! (also because i dont know GDScript)

Because of this emulation to run the server you must run it from a steam account that owns the game and that has steam open in the background

## If you have any questions or issues with Cove, create and issue on Github or DM me on Discord `meepso`

# Todo
- [X] Spawn actors required for the metal detector
- [X] Improve error handling
- [X] Some sort of plugin or modding support (C# API)
- [ ] Add proper support for actor handling (90%)
- [X] Make hostspawn and metalspawn IHostedService's
- [ ] Write a plugin guide / how to create plugins

# How to run:

> [!NOTE]  
> To run a server you must have Steam open on the computer you wish to run the server on
> and Steam must be logged into a account that has WebFishing in it's library 
> 
> Also please note you can't join the server on the account you are hosting it on!

1. Download
	- You can download the most recent version of the server here: [Nightly Releases](https://github.com/DrMeepso/WebFishingCove/tags)
	- Or if you want the latest stable version it is here: [Latest Release](https://github.com/DrMeepso/WebFishingCove/releases/latest)
	- A new build is made everytime code is changed so it may update quite alot!

2. Decompile WebFishing
	- Once you have the source files drag the `main_zone.tscn` file into the `/worlds` folder of the server!
	- The `main_zone.tscn` file can be found here in the WebFishing project `/Scenes/Map`

3. Change settings
	- If you dont see the config files (server.cfg & admins.cfg) run the server once and they should be created in the same place the application is!
	- You can modify the settings in the server.cgf file with all the info you want!
	- Too add a admin put there Steam64ID in the admins.cfg file with a ` = true` after it!
	- I.E. `76561198288728683 = true`

4. Run!
	- Run the server EXE and enjoy! 
	- Please be respectful and dont name the servers anything stupid!

	 
# Other info

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/E1E0E65CR)

All donations are greatly appreciated!!!!!!!! <3 :3
