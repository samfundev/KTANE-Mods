---
title: Tweaks
---
# Features
This list is sorted with what I think are generally the most useful and relevant features first.
* Adds demand-based mod loading, making the game boot up faster and saving memory. See the [DBML](#demand-based-mod-loading) section below.
* Picks the smallest case that fits the number of modules on the bomb.
* Adds a customizable case generator which will create the best fitting case.
* Adds three new ways to solve bombs: Time, Zen and Steady Mode. See the [Modes](#Modes) section below.
* Adds two separate HUDs, one showing the bomb status and the other showing edgework.
* Adds a mod settings editor app to Mod Selector.
* Allows the player to hide table of contents entries in the mission binder. [(See example.)](https://i.imgur.com/pN0WfJw.gif)
* Adds a setting to control the mission seed. So you can try to consistently generate the same bomb.
* Adds a setting to turn the Mods Only key on by default.
* Adds a setting to instantly skip the gameplay loading screen and or the lights being out at the start of a bomb.
* Sets the fade time to be the same between scenes and which can be controlled with a setting.
* Adds logging for many modules like Probing and Keypad.
* Adds logging which is used to improve the [Logfile Analyzer](https://ktane.timwi.de/More/Logfile%20Analyzer.html).
* Fixes Word Scramble so that the word "papers" is accepted.
* Fixes the URL that Foreign Exchange Rates uses since the old one no longer works.
* Removes the maximum time limit in the freeplay briefcase.
* Makes the minimum number of modules in the freeplay briefcase to 1.
* Fixes little details about modules like centering the status light or background.

# Settings
Most of these settings can be easily edited using the Mod Settings Editor found in the Mod Selector tablet. Additional ones can found in a file called "TweakSettings" in your "Modsettings" folder.
* <b id="settings-FadeTime">Fade Time</b> (Default: `1`) - The number seconds should it take to fade in and out of scenes. If the is negative then the game default's will not be changed.
* <b id="settings-InstantSkip">Instant Skip</b> (Default: `true`) - Skips the gameplay loading screen as soon as possible.
* <b id="settings-SkipGameplayDelay">Skip Gameplay Delay</b> (Default: `false`) - Skips the delay at the beginning of a round when the lights are out.
* <b id="settings-BetterCasePicker">Better Case Picker</b> (Default: `true`) - Chooses the smallest case that fits instead of a random one.
* <b id="settings-EnableModsOnlyKey">Enable Mods Only Key</b> (Default: `false`) - Turns the Mods Only key to be on by default.
* <b id="settings-DemandBasedModLoading">Demand Based Mod Loading</b> (Default: `false`) - Load only the modules on a bomb instead of loading all of them when starting up. See the [DBML](#demand-based-mod-loading) section below.
* <b id="settings-DisableDemandBasedMods">Disable Demand Based Mods</b> (Default: `false`) - Disables mods that can be loaded on demand so they aren't load when the game starts. This tricks the game into thinking that you've disabled these mods in the Mod Manager so this cannot be undone without manually re-enabling these mods.
* <b id="settings-DemandModLimit">Demand Mod Limit</b> (Default: `-1`) - Sets the limit of how many mods will be kept loaded after the bomb is over. Negative numbers will keep all mods loaded..
* <b id="settings-FixFER">Fix FER</b> (Default: `false`) - Changes the URL that is queried by [Foreign Exchange Rates](https://steamcommunity.com/sharedfiles/filedetails/?id=743442301) since the old one is no longer operational. See the module’s [manual](https://ktane.timwi.de/HTML/Foreign%20Exchange%20Rates.html) for the new URL.
* <b id="settings-BombHUD">Bomb HUD</b> (Default: `false`) - Adds a HUD in the top right corner showing information about the currently selected bomb.
* <b id="settings-ShowEdgework">Show Edgework</b> (Default: false) - Adds a HUD to the top of the screen showing the edgework for the currently selected bomb.
* <b id="settings-DisableAdvantageous">Disable Advantageous</b> (Default: `false`) - Disables [advantageous features](#advantageous-features) so you don't have to modify your settings temporarily.
* <b id="settings-ShowTips">Show Tips</b> (Default: `true`) - Shows tips about Tweaks features that you may not know about. Only tips about features that aren't being used will be shown.
* <b id="settings-HideTOC">Hide TOC</b> (Default: `[]`) - Hides table of contents entries that match the specified patterns. [(See example.)](https://i.imgur.com/pN0WfJw.gif) Patterns can use an `*` to match any number of characters and a `?` to match any single character. The pattern must match the whole entry and is case-insensitive. Patterns must be comma separated. Example:
```json
"HideTOC": [
    "Probing",
    "Hexi*",
    "*Translated*"
]
```
* <b id="settings-Mode">Mode</b> (Default: `"Normal"`) - Sets the mode for the next bomb. Acceptable values are "Normal", "Zen", "Time" and "Steady". The [Bomb Creator](https://steamcommunity.com/sharedfiles/filedetails/?id=1224340893) allows you to change the current mode. See the [Modes](#Modes) section below.
* <b id="settings-MissionSeed">Mission Seed</b> (Default: `-1`) - Seeds the random numbers for the mission which should make the bomb generate consistently. Useful for trying to race to see who could complete a bomb first. You'll need to make sure things are consistent like settings and what mods are installed. A negative number means that the random numbers won't be seeded by Tweaks.
* <b id="settings-CaseGenerator">Case Generator</b> (Default: `true`) - Generates a case to best fit the number of modules on the bomb which can be one of the colors defined by `CaseColors`. Requires BetterCasePicker to be enabled.
* <b id="settings-ModuleTweaks">Module Tweaks</b> (Default: `true`) - Controls all module related tweaks like fixing status light positions.
* <b id="settings-CaseColors">Case Colors</b> (Default: `[]`) - Colors which can be picked by the case generator. Supports: Common color names, HEX, RGB, HSV and random. Example: 
```json
"CaseColors": [
    "red",
    "#FF0000",
    "rgb(255, 0, 0)",
    "hsv(0, 255, 255)",
    "red-green-blue",
    "random"
]
```
`red`, `#FF0000`, `rgb(255, 0, 0)` and `hsv(0, 255, 255)` show four different formats but are all the same color. You can use a minus sign (-) to specify a range between colors. So `red-green-blue` would pick any color between red and green or between green and blue. `random` will be a random color every time.

# Advantageous Features
If you enable BombHUD, ShowEdgework, use a Mode other than Normal or a MissionSeed other than -1 you won't be able to set records on leaderboards or change your stats.

# Modes
To help you identify the mode you are playing with, the timer changes to a unique color. For Normal mode the color is red, for Zen mode it’s cyan, for Time mode it’s orange, and for Steady mode it's green. The [Bomb Creator](https://steamcommunity.com/sharedfiles/filedetails/?id=1224340893) allows you to change the mode and the initial time for Time mode. Because this changes the values in the settings files, they are used regardless of how a bomb is started.

* **Normal mode** is the mode that comes with the game. The timer counts down and you have a fixed number of strikes to solve a bomb.
* **Zen mode** changes two things compared to normal mode. The first change is that the timer starts at 0:00 and count up. Despite this, the initial time for the bomb is still considered to be the same. The other change is that strikes cannot make the bomb explode. Strikes are still recorded and otherwise act the same as normal mode. You can configure how much strikes cause the timer to speed up and/or make it give a time penalty.
* **Time mode** is a different way to solve bombs that adds time to the timer as you solve modules. When you solve a module, the number of seconds added is the product of the point value of the module and the current multiplier. As you solve modules your multiplier goes up, but if you get a strike your multiplier is reduced and you lose a percentage of your time.
It is important for Time mode that each module be given a point value based on the time it takes to solve the module. This way longer modules will add more time to the clock and shorter modules will add less time. Because new modules come out all the time,  you must do regular maintenance to keep the point values up to date. Modules that do not have a set point value will be treated as if they were set to 6 points.
* **Steady mode** is very similar to Normal mode but the timer doesn't speed up but instead a time penalty is given for getting a strike.

# Mode Settings
The settings for all the modes are found in a file called "ModeSettings" in your "Modsettings" folder.
An [editor](https://ktane.timwi.de/More/Mode%20Settings%20Editor.html) has been created so you can modify this file easily. It can also be found on the [Repository of Manual Pages](https://ktane.timwi.de), under the “More” tab. To make maintaining the ModeSettings file easier, the editor is split up into sections and allows you to sort the list of modules three ways: alphabetically, by score, or by release date.

Initially the Mode Settings Editor will be populated with the default TP point values for all modules that have them. Modules that don’t have a point value set will be outlined in red. After the point value there may be a number in parenthesis. This is the point difference that module gives when compared to the default Twitch Plays value. If it is a “?” instead of a number, then there is no Twitch Plays value for that module. If you are confused, you can always hover over it for an explanation.

The ModeSettings file contains the following settings:
## Zen Mode
* **Time Penalty** (Default: `0`) - The base amount of minutes to be penalized for getting a strike.
* **Time Penalty Increase** (Default: `0`) - The number of minutes to add to the penalty each time you get a strike after the first.
* **Timer Speed Up** (Default: `0.25`) - The rate the timer speeds up when you get a strike. For example, if this value is 0.25 then the timer speeds up to 1.25x for the first strike, 1.5x for the second, etc.
* **Timer Max Speed** (Default: `2`) - The maximum rate the timer can be set to. For example, 2 is twice as fast as the normal timer.
## Steady Mode
* **Fixed Penalty** (Default: `2`) - The number of minutes subtracted from the time when you get a strike.
* **Percent Penalty** (Default: `0`) - The factor of the starting time the remaining time is reduced by. For example, if this value is 0.25 and you get a strike with 10 minutes on the clock, then the timer will have 7 minutes and 30 seconds left.
## Time Mode
* **Starting Time** (Default: `5`) - The number of minutes on the timer when you start a bomb. This can be set in the game by using [Bomb Creator](https://steamcommunity.com/sharedfiles/filedetails/?id=1224340893) when Time mode is enabled.
* **Starting Multiplier** (Default: `9`) - The initial multiplier.
* **Max Multiplier** (Default: `10`) - The highest the multiplier can go.
* **Min Multiplier** (Default: `1`) - The lowest the multiplier can go.
* **Solve Bonus** (Default: `0.1`) - The amount added to the multiplier when you solve a module.
* **Multiplier Strike Penalty** (Default: `1.5`) - The amount subtracted from the multiplier when you get a strike.
* **Timer Strike Penalty** (Default: `0.25`) - The factor the time is reduced by when getting a strike. For example, if this value is 0.25, then after a strike the timer will have 75% it's previous value.
* **Minimum Time Lost** (Default: `15`) - Lowest amount of time that you can lose when you get a strike.
* **Minimum Time Gained** (Default: `20`) - Lowest amount of time you can gain when you solve a module.
* **Point Multiplier** (Default: `1`) - The additional multiplier for all points earned.
* **Component Values** (Default: `{}`) - The base point value for each module specified by its module ID. Example:
```json
"ComponentValues": {
    "MemoryV2": 5.0,
    "HexiEvilFMN": 7.0,
    "SkewedSlotsModule": 10.0,
    "CheapCheckoutModule": 12.0
}
```
* **Total Modules Multiplier** (Default: `{}`) - The bonus points awarded for solving a module specified by its module ID. This value is multiplied by the number of modules on the bomb. The point value for a module is the base points plus these bonus points. This feature is especially suitable for modules like Forget Me Not or Souvenir. Unfortunately, at this time, this field can’t be changed using the Mode Settings editor. Example:
```json
"TotalModulesMultiplier": {
    "MemoryV2": 0.5,
    "HexiEvilFMN": 1.5
}
```

# Demand-based Mod Loading
Demand-based mod loading makes it so that modules are only loaded into the game once they are needed on a bomb. This means that not only is the time to start the game up reduced significantly but also the game will use less memory.

The simplest way to enable DBML is to enable both the ["Demand-based Mod Loading"](#settings-DemandBasedModLoading) and the ["Disable Demand-based Mods"](#settings-DisableDemandBasedMods) feature then follow the prompt. Keep in mind that the second feature isn't easily reversed without manually re-enabling these mods. Which you would only need to reverse if you wanted to not load all modules using through DBML.

The reason the second feature is recommended to be enabled along with the first is that Tweaks will only load modules on demand if they aren't already loaded into the game. So it saves a lot of effort of having to go through and disable many modules so that they can be used with DBML.