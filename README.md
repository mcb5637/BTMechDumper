# BTMechDumper

Mod for BattleTech

Utlility tool to read mech and mechpart data from the game and write it into a easy readable txt format and a few csv files.
The files are written every time you enter the Mechbay on your Argo/Leopard.
BTDump.txt file :
Consists of multiple parts, first the company name and date, followed by a mech list (the + and - in front of a mechs name shows if you already have one of this type),
followed by all weapons types, all ammobox types, and last all other components together (upgrades, heatsinks & jumpjets).

BTDumpMechs.csv, BTDumpWeapons.csv, BTDumpAmmo.csv, BTDumpUpgrades.csv files:
The same data as in BTDump.txt, but splitted into multiple files and in an handy csv format, readable by programms like OpenOffice or Microsoft Excel.
(To read them properly, on import, set fieldseparator to only semicolon (;) and textseparator to ")

Note on Mechs: Pure AI mechs are not shown, to show up in the list, the mech has to not be blacklisted
or contained in an itemcollectiondef (for shops/flashpoint rewards or similar).
This makes the BIG STEEL CLAW contained in the list, but Victorias King Crab not.
Mechparts should be in the list, regardless of blacklisted status.

Note on MechEngineer Mod:
As far as i know MechEngineer creates Mechs at runtime, and for this reason BTMechDumper could read out completely wrong walues (or miss mechs entirely) when used with MechEngineer.

Note on CustomAmmoCategories & Dependent Mods:
The custom Ammos show up in the list, but their boni are only shown in the vanilla boni fields. So some of the info might not be accurate.
Additionally each ammotype shows up as a separate ammotype and so the weapons ammotype and the ammoboxes ammotype don't match.
Alternate firing modes also don't show up in the list.