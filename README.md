[![Github Downloads](https://img.shields.io/github/downloads/FangkuaiYa/Among-Chess-R/total.svg)](https://github.com/FangkuaiYa/Among-Chess-R/releases)
[![GitHub release](https://img.shields.io/github/release/FangkuaiYa/Among-Chess-R.svg)](https://GitHub.com/FangkuaiYa/Among-Chess/releases/)

![Among Chess Logo](https://github.com/FangkuaiYa/Among-Chess-R/blob/master/Images/Logo-3D-Faded.png "Among Chess")

---
# Table of Contents
1. [About](https://github.com/FangkuaiYa/Among-Chess-R/README.md#About "About Section")
2. [Disclaimer](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Disclaimer "Disclaimer Section")
3. [Installation](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Installation "Installation Section")
4. [Feedback](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Feedback "Feedback Section")
5. [Credit](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Credit "Credit Section")
6. [Settings](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Settings "Settings Section")
    1. [Game Modes](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Game-Modes "Game Modes Subsection")
    2. [Variation](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Variation "Variation Subsection")
    3. [Board](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Board "Variation Subsection")
    4. [Main Time](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Main-Time "Main Time Subsection")
    5. [Increment Time](https://github.com/FangkuaiYa/Among-Chess-R/README.md#Increment-Time "Increment Time Subsection")

---
# About
Among Chess is an Among Us mod for playing chess. In the in-game settings there are various variants, boards, and time controls you may play with. All traditional rules of chess applies to Among Chess.\
For a fun 1 minute trailer, click [this link](https://www.youtube.com/watch?v=44SC-SNaBDg).

![Among Chess Gameplay](https://github.com/FangkuaiYa/Among-Chess-R/blob/master/Images/Gameplay-Board.png "Among Chess Gameplay")

---
# Disclaimer
<p align="center">This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC.</p>

<p align="center">© Innersloth LLC.</p>

---
# Installation

The mod can be downloaded two different ways. By directly downloading the mod which is easiest but doesn't automatically update, or by downloading mod manager which is harder but automatically updates the mod and adds a few other mods.

### Direct Download

| Mod Version| Among Us Version| Download|
|:---|:---|:---:|
| *Latest*(1.2.0-17.4)|   | [Link](https://github.com/FangkuaiYa/Among-Chess-R/releases/latest "Latest Version")||
| 1.1.1| 2021.6.30| [Link](https://github.com/FangkuaiYa/Among-Chess-R/releases/tag/v1.1.1 "Version 1.1.1")|
| 1.0.3| 2021.6.15| [Link](https://github.com/FangkuaiYa/Among-Chess-R/releases/tag/v1.0.3 "Version 1.0.3")|

For a visual guide, view [this tutorial](https://www.youtube.com/watch?v=JCvxKicRfB4) made by *DaNOOB*.

1. Download the mod using the download link above, then extract the Among Chess folder.
2. Go to the location of your Among Us folder and copy the folder to an easily accessible location (such as the documents folder).
    1. If you bought Among Us through Steam, open Steam and locate Among Us in your library, select properties then local files.
    2. If you bought Among Us through Epic Games, open file explorer, navigate to Program Files, find Among Us.
3. Move the folder Among Chess folder's contents to the new Among Us folder.

The folder should look like this after step 3 (assuming you are modding the Steam version).
```
.
┣━ Among Us_Data
┣━ BepInEx*
┣━ mono*
┣━ Among Us
┣━ baselib.dll
┣━ doorstop_config*
┣━ GameAssembly.dll
┣━ msvcp140.dll
┣━ UnityCrashHandler32
┣━ UnityPlayer.dll
┣━ vcruntime140.dll
┗━ winhttp.dll*
```

### Mod Manager

Follow the instructions on the [Mod Manager's website](https://mm.matux.fr "Mod Manager's Website").

---
# Feedback
If you have any feedback regarding the game, this may include: 
- Bugs
- Suggestions
- Support
- Inquiries

Please contact the current creator by messaging him on 2683748223@qq.com The mod creator cannot be bothered and is always happy to respond to any feedback.

---
# Credit
| Title| Description| Links|
|:---|:---|:---|
| Harmony| C# patching library| [Github page](https://github.com/pardeike/Harmony "Github Page"), [Documentation](https://harmony.pardeike.net/ "Documentation")|
| BepInEx| Unity patching library| [Github page](https://github.com/BepInEx/BepInEx "Github Page")|
| Reactor| Among Us modding API| [Github page](https://github.com/NuclearPowered/Reactor "Github Page"), [Documentation](https://docs.reactor.gg/ "Documentation"), [Discord](https://discord.com/invite/pKM7pbufP3 "Discord Server")|

---
# Settings

```
.
┣━┯━ Game Modes
┃ └─── Chess
┃
┣━┯━ Variation
┃ ├─── Normal
┃ └─── Real-Time ┄ (Both players may move at the same time)
┃
┣━┯━ Board
┃ ├─── Default
┃ ├─── Chess960 ┄ (Randomized positions of pieces, placement is mirrored)
┃ └─── Transcendental Chess ┄ (Randomized positions of pieces, placement is not mirrored)
┃
┗━┳━ Time Control ┅ [Note 1]
  ┣━┯━ Main Time
  ┃ ├─── Unlimited ┄ (Correspondence)
  ┃ ├─── 0.5 ┄ (Bullet)
  ┃ ├─── 1 ┄ (Bullet)
  ┃ ├─── 2 ┄ (Bullet)
  ┃ ├─── 3 ┄ (Blitz)
  ┃ ├─── 5 ┄ (Blitz)
  ┃ ├─── 10 ┄ (Rapid)
  ┃ ├─── 30 ┄ (Rapid)
  ┃ └─── 60 ┄ (Rapid)
  ┃
  ┗━┯━ Increment Time ┅ [Note 2]
    ├─── 0
    ├─── 0.5
    ├─── 1
    ├─── 2
    ├─── 5
    ├─── 10
    └─── 30
```

[Note 1]: Time control can get slightly desynchronized. The mod will attempt to adjust for it the best it can.\
[Note 2]: The duration it takes to walk between pieces is already considered. Change these settings like you would on a standard chess website.
