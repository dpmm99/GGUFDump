# GGUFDump
This repository contains two similar apps, one C# and one HTML+JS, for dumping GGUF data, calculating KV cache usage, and suggesting layers to GPU-offload for Mixture-of-Experts (MoE) models. (Note: it's not looking at the experts' activation frequency or anything, just giving a basic recommendation by looking at metadata.)

The C# console app supports one file at a time. Just drag and drop a file on the EXE or use the command line to give it a file path/name and it spits all the data out in the console.

The Web app supports multiple files at once. You can drag and drop them anywhere on the page or use the file picker. It presents the data in collapsible sections, so it's a bit nicer than the C# app.

Since they both implement GGUF parsing from scratch, they only load the headers, so you can use these in your own projects if you need to quickly read GGUF metadata.

This project is licensed under the MIT License because some code was adapted from [llama.cpp](https://github.com/ggerganov/llama.cpp), since the tool's sole purpose is to read files in the GGUF format created by Georgi Gerganov.
