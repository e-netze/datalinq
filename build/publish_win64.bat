echo off

cd .\..\src\web\DataLinq.Api
dotnet build -c Release -p:Configuration=Release -p:DeployOnBuild=true -p:PublishProfile=win64

if errorlevel 1 goto error

cd .\..\DataLinq.Code
dotnet build -c Release -p:Configuration=Release -p:DeployOnBuild=true -p:PublishProfile=win64

if errorlevel 1 goto error

echo ==================
echo Publish Successful
echo ==================

goto end

:error
echo *****************
echo An error occurred
echo *****************

pause

:end