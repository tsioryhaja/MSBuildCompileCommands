# MSBuildCompileCommands

This was made to generate a compilation data for msvc project using MSBuild as build tool.
It actually mimic the "DesignTime" build that visual studio use to manage it's intellisense.
So it will need the .NetFramework environment.

# Build

To build you would need the .NetFramework environment from Visual studio (you can install only the command line).
I'm not a .Net expert so I'm not sure it will be compatible with .Net core but I'm gonna try to see what I can do.
You will also need to install _Go_ to build it as there is a secod executable written in go that will format the compilation database.
I used go 1.21.

To build the project, there is a powershell script called "_build.ps1_" that actually do the build and bring alll of the executable to the same folder.

You have to execute this build from a Powershell instance that is configured to use the Microsoft build (like the visual studio powershell).

```
./build.ps1
```

this will create a "bin" folder at the root of the project containing a lot of things.

# Use

To use it, you should call the "_MSBuildCompileCommand.exe_" executable in the newly generated bin folder with your project's "_.sln_" or "_.vcxproj_" file as parameter.
Example

```
MSBuildCompileCommand.exe project.vcxproj
```

# Information

I'm still working to add a way to add configuration and global properties you can set on the command line.
I also need to check if it work with .Net Core.