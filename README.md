# relativity-rsmf-gen-sample
Sample code that demonstrates how to construct an RSMF file using open source libraries.
## Introduction
The sample code in this project deomonstrates how to construct an RSMF file from an rsmf\_manifest.json file has already been created.  It uses Newtonsoft.Json and MimeKit Lite to parse the JSON and create the RSMF EML layer.

## Build
To build the sample code, open the RSMFGen.sln in Visual Studio 2015.  Restore the Nuget packages to download the required libraries.  Build in Release or Debug mode.

## Running RSMFGen.exe
RSMFGen.exe is already built and can be run from the bin\Release directory.  As its parameters it takes in a directory that contains an rsmf\_manifest.json file and a path to an output file.  Any other files in the input directory will also be included in the generated RSMF file.  The rsmf\_manifest.json file can be validated by providing -validate as a third optional parameter.

## RSMFGenLib.dll
This library contains all of the code that is used to generate an RSMF file.  It can be referenced in other applications.  It requires NewtonSoft.Json.dll, NJsonSchema.dll, Relativity.RSMFU.Validator.dll and MimeKitLite.dll to run, so references to those libraries are also requried.