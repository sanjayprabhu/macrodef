NAnt Macrodef Task
by Eoin Curran (http://peelmeagrape.net/) with the support of Exoftware (http://www.exoftware.com/)

Addon task for NAnt (http://nant.sourceforge.net/) that allows for defining new tasks, similarly to ant (http://ant.apache.org/).

Licensed under GPL - see COPYING.txt


Update:
Whenever it comes across a new macrodef, it will SHA the content to generate a unique identifier. If a DLL with that name exists in the temp directory, it will use that DLL instead of recompiling it. If not, it will compile the macrodef and save it with the unique identifier.

What this means is that all macrodefs will be compiled only once and should be significantly faster to load. It should detect changes and recompile them automatically.

If you need to delete the cached DLLs, you'll find them in %temp% prefixed with "mdef_"