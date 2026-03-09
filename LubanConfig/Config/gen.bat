set WORKSPACE=..
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=.
set CODE_OUT_PATH=..\..\Assets\Game\Scripts\Runtime\Hotfix\Config
set DATA_OUT_PATH=..\..\Assets\ResBundle\Config

dotnet %LUBAN_DLL% ^
    -t all ^
    -c cs-bin ^
    -d bin ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%CODE_OUT_PATH% ^
    -x outputDataDir=%DATA_OUT_PATH%

pause