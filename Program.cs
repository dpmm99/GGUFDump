using LLMExperiment;

namespace GGUFDump;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length >= 1)
        {
            //Use args as the model filename, use GgufMetadata to load it, and just dump all the dictionaries to the screen.
            //Join all args together with a space in case the filename has spaces in it and the user didn't use quotation marks.
            DumpGGUF(string.Join(" ", args));
        }
        else
        {
            Console.WriteLine("Usage: GGUFDump <filename>. Alternatively, you can drag and drop a GGUF file onto the executable.");
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    private static void DumpGGUF(string filename)
    {
        var ggufMetadata = new GgufMetadata(filename);
        foreach (var dictEntry in ggufMetadata.BoolValues)
        {
            Console.WriteLine($"BoolValues[{dictEntry.Key}] = {dictEntry.Value}");
        }

        foreach (var dictEntry in ggufMetadata.Float32Values)
        {
            Console.WriteLine($"Float32Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.Float64Values)
        {
            Console.WriteLine($"Float64Values[{dictEntry.Key}] = {dictEntry.Value}");
        }

        foreach (var dictEntry in ggufMetadata.Int8Values)
        {
            Console.WriteLine($"Int8Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.Int16Values)
        {
            Console.WriteLine($"Int16Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.Int32Values)
        {
            Console.WriteLine($"Int32Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.Int64Values)
        {
            Console.WriteLine($"Int64Values[{dictEntry.Key}] = {dictEntry.Value}");
        }

        foreach (var dictEntry in ggufMetadata.UInt8Values)
        {
            Console.WriteLine($"UInt8Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.UInt16Values)
        {
            Console.WriteLine($"UInt16Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.UInt32Values)
        {
            Console.WriteLine($"UInt32Values[{dictEntry.Key}] = {dictEntry.Value}");
        }
        foreach (var dictEntry in ggufMetadata.UInt64Values)
        {
            Console.WriteLine($"UInt64Values[{dictEntry.Key}] = {dictEntry.Value}");
        }

        foreach (var dictEntry in ggufMetadata.StringValues)
        {
            Console.WriteLine($"StringValues[{dictEntry.Key}] = {dictEntry.Value}");
        }

        Console.WriteLine("Tensors in GPU-offloading priority order:");
        foreach (var tensor in ggufMetadata.GetTensorsForOffload())
        {
            Console.WriteLine($"{tensor.Name} ({tensor.Type}, dimensions: [{string.Join(", ", tensor.Dimensions)}])");
        }

        Console.WriteLine("KV cache usage per token: " + ggufMetadata.GetKvCacheKBPerToken() + " KB");
    }
}
