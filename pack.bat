del ".\BTMechDumper.zip"
mkdir .\tmp\BTMechDumper
copy .\BTMechDumper\bin\Release\BTMechDumper.dll .\tmp\BTMechDumper
copy .\BTMechDumper\mod.json .\tmp\BTMechDumper
cd .\tmp
"C:\Program Files\7-Zip\7z.exe" a "..\BTMechDumper.zip" "BTMechDumper\" "..\LICENSE" "..\README.md"
cd ..\
rmdir /s /q .\tmp
pause