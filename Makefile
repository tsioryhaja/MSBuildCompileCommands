build:
	cd generator && go build
	cd compile && msbuild compile.sln -t:Restore -p:RestorePackagesConfig=true && msbuild compile.sln
	mkdir bin
	Xcopy compile\compile\bin\Debug bin /E /H /C /I
	copy generator\compilecommand.exe bin\
