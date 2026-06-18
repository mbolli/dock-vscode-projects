# dock/ — PowerToys Command Palette dock extension

C# / .NET. Built on Windows over `\\wsl.localhost\Ubuntu\var\www\dock-vscode-projects`
(no dotnet in WSL; CmdPal is Windows-only). Reads the shared state directory, renders
the band, owns favourites, and performs focus/launch actions per `../contract/`.

Scaffolded in Group 2 of the kickoff plan from the CmdPal Extensions template
(short-name confirmed via `dotnet new list cmdpal`), targeting SDK 0.11.
