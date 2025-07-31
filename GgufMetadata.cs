using System.Text;
using System.Text.Json;

namespace LLMExperiment;

/// <summary>
/// Loads metadata (excluding arrays) and tensor metadata from a GGUF v3 file according to the specification at https://github.com/ggerganov/ggml/blob/master/docs/gguf.md
/// </summary>
public class GgufMetadata
{
    //Define a Dictionary<string, T> for each datatype T in the file specification, to maintain strong typing, because this is the world of .NET
    public Dictionary<string, byte> UInt8Values { get; } = [];
    public Dictionary<string, sbyte> Int8Values { get; } = [];
    public Dictionary<string, ushort> UInt16Values { get; } = [];
    public Dictionary<string, short> Int16Values { get; } = [];
    public Dictionary<string, uint> UInt32Values { get; } = [];
    public Dictionary<string, int> Int32Values { get; } = [];
    public Dictionary<string, float> Float32Values { get; } = [];
    public Dictionary<string, bool> BoolValues { get; } = [];
    public Dictionary<string, string> StringValues { get; } = [];
    public Dictionary<string, ulong> UInt64Values { get; } = [];
    public Dictionary<string, long> Int64Values { get; } = [];
    public Dictionary<string, double> Float64Values { get; } = [];

    /// <summary>
    /// For use at design-time to help you figure out the appropriate types for the values you're seeking.
    /// </summary>
    public Dictionary<string, Type> ValueTypes { get; } = [];

    public IReadOnlyList<TensorInfo> Tensors { get; private set; } = [];

    public GgufMetadata(string filename)
    {
        using var br = new BinaryReader(File.OpenRead(filename));
        if (br.ReadUInt32() != 0x46554747) // 'GGUF' in little-endian
        {
            throw new ArgumentException("Not a GGUF file.", nameof(filename));
        }

        var version = br.ReadInt32();
        if (version != 3) throw new ArgumentException($"Unsupported GGUF version {version}. This class only supports v3.", nameof(filename));

        var tensorCount = br.ReadUInt64();

        var metadataCount = br.ReadUInt64();
        for (ulong x = 0; x < metadataCount; x++)
        {
            //Load metadata values, but only strings and single values.
            ReadMetadataKeyValuePair(br);
        }

        var tensors = new List<TensorInfo>((int)tensorCount);
        for (ulong i = 0; i < tensorCount; i++)
        {
            var name = ReadString(br);
            var nDimensions = br.ReadUInt32();
            var dimensions = new ulong[nDimensions];
            for (uint d = 0; d < nDimensions; d++)
            {
                dimensions[d] = br.ReadUInt64();
            }
            var type = (GgmlType)br.ReadUInt32();
            var offset = br.ReadUInt64();

            tensors.Add(new TensorInfo
            {
                Name = name,
                Dimensions = dimensions,
                Type = type,
                Offset = offset
            });
        }
        Tensors = tensors;

        br.Close();
    }

    private void ReadMetadataKeyValuePair(BinaryReader br)
    {
        var key = ReadString(br);
        var valueType = (GgufMetadataValueType)br.ReadUInt32();

        switch (valueType)
        {
            case GgufMetadataValueType.UINT8:
                UInt8Values[key] = br.ReadByte();
                ValueTypes[key] = typeof(byte);
                break;
            case GgufMetadataValueType.INT8:
                Int8Values[key] = br.ReadSByte();
                ValueTypes[key] = typeof(sbyte);
                break;
            case GgufMetadataValueType.UINT16:
                UInt16Values[key] = br.ReadUInt16();
                ValueTypes[key] = typeof(ushort);
                break;
            case GgufMetadataValueType.INT16:
                Int16Values[key] = br.ReadInt16();
                ValueTypes[key] = typeof(short);
                break;
            case GgufMetadataValueType.UINT32:
                UInt32Values[key] = br.ReadUInt32();
                ValueTypes[key] = typeof(uint);
                break;
            case GgufMetadataValueType.INT32:
                Int32Values[key] = br.ReadInt32();
                ValueTypes[key] = typeof(int);
                break;
            case GgufMetadataValueType.FLOAT32:
                Float32Values[key] = br.ReadSingle();
                ValueTypes[key] = typeof(float);
                break;
            case GgufMetadataValueType.BOOL:
                BoolValues[key] = br.ReadByte() != 0;
                ValueTypes[key] = typeof(bool);
                break;
            case GgufMetadataValueType.STRING:
                StringValues[key] = ReadString(br);
                ValueTypes[key] = typeof(string);
                break;
            case GgufMetadataValueType.UINT64:
                UInt64Values[key] = br.ReadUInt64();
                ValueTypes[key] = typeof(ulong);
                break;
            case GgufMetadataValueType.INT64:
                Int64Values[key] = br.ReadInt64();
                ValueTypes[key] = typeof(long);
                break;
            case GgufMetadataValueType.FLOAT64:
                Float64Values[key] = br.ReadDouble();
                ValueTypes[key] = typeof(double);
                break;
            case GgufMetadataValueType.ARRAY:
                //Skip arrays entirely.
                SkipArray(br);
                break;
            default:
                throw new ArgumentException($"Unknown metadata value type: {valueType}");
        }
    }

    private static string ReadString(BinaryReader br)
    {
        var length = br.ReadUInt64(); //Number of bytes, not characters. But we can't support the full fidelity of the field 'cuz RAM isn't in exabytes and ReadBytes only accepts a signed int. :)
        return Encoding.UTF8.GetString(br.ReadBytes((int)length));
    }

    private static void SkipString(BinaryReader br)
    {
        var length = br.ReadUInt64();
        br.BaseStream.Position += (long)length;
    }

    private static void SkipArray(BinaryReader br)
    {
        var elementType = (GgufMetadataValueType)br.ReadUInt32();
        var length = br.ReadUInt64();
        for (ulong i = 0; i < length; i++)
        {
            SkipValue(br, elementType);
        }
    }

    private static void SkipValue(BinaryReader br, GgufMetadataValueType valueType)
    {
        switch (valueType)
        {
            case GgufMetadataValueType.UINT8:
            case GgufMetadataValueType.INT8:
            case GgufMetadataValueType.BOOL:
                br.ReadByte();
                break;
            case GgufMetadataValueType.UINT16:
            case GgufMetadataValueType.INT16:
                br.ReadUInt16();
                break;
            case GgufMetadataValueType.UINT32:
            case GgufMetadataValueType.INT32:
            case GgufMetadataValueType.FLOAT32:
                br.ReadUInt32();
                break;
            case GgufMetadataValueType.UINT64:
            case GgufMetadataValueType.INT64:
            case GgufMetadataValueType.FLOAT64:
                br.ReadUInt64();
                break;
            case GgufMetadataValueType.STRING:
                SkipString(br);
                break;
            case GgufMetadataValueType.ARRAY:
                SkipArray(br);
                break;
            default:
                throw new ArgumentException($"Unknown metadata value type: {valueType}");
        }
    }

    enum GgufMetadataValueType : uint
    {
        UINT8 = 0,
        INT8 = 1,
        UINT16 = 2,
        INT16 = 3,
        UINT32 = 4,
        INT32 = 5,
        FLOAT32 = 6,
        BOOL = 7,
        STRING = 8,
        ARRAY = 9,
        UINT64 = 10,
        INT64 = 11,
        FLOAT64 = 12
    }

    public enum GgmlType : uint
    {
        F32 = 0,
        F16 = 1,
        Q4_0 = 2,
        Q4_1 = 3,
        // Q4_2/3 removed
        Q5_0 = 6,
        Q5_1 = 7,
        Q8_0 = 8,
        Q8_1 = 9,
        Q2_K = 10,
        Q3_K = 11,
        Q4_K = 12,
        Q5_K = 13,
        Q6_K = 14,
        Q8_K = 15,
        IQ2_XXS = 16,
        IQ2_XS = 17,
        IQ3_XXS = 18,
        IQ1_S = 19,
        IQ4_NL = 20,
        IQ3_S = 21,
        IQ2_S = 22,
        IQ4_XS = 23,
        I8 = 24,
        I16 = 25,
        I32 = 26,
        I64 = 27,
        F64 = 28,
        IQ1_M = 29,
    }

    /// <summary>
    /// Represents metadata information about a tensor, including its name, type, dimensions, and offset.
    /// </summary>
    public class TensorInfo
    {
        public required string Name { get; init; }
        public required GgmlType Type { get; init; }
        public required ulong[] Dimensions { get; init; }
        public required ulong Offset { get; init; }

        /// <summary>
        /// Extracts the block number from the <see cref="Name"/> property, e.g., from "blk.12.attn_norm.weight" -> 12
        /// </summary>
        /// <remarks>The method expects the <see cref="Name"/> to start with "blk." followed by the block
        /// number. If the format is not followed, or the block number cannot be parsed, the method returns
        /// -1.</remarks>
        /// <returns>The block number extracted from the <see cref="Name"/> if the format is correct; otherwise, -1.</returns>
        public int GetBlockNumber()
        {
            if (!Name.StartsWith("blk.")) return -1;
            var parts = Name.Split('.');
            if (parts.Length > 1 && int.TryParse(parts[1], out var blockNum))
            {
                return blockNum;
            }
            return -1;
        }
    }

    /// <summary>
    /// Gets the list of tensors sorted in a hopefully optimal order for offloading to a GPU with limited VRAM.<br/>
    /// The priority is:<br/>
    /// 1. Foundational shared layers (embeddings, output norm, etc.).<br/>
    /// 2. Standard transformer block shared layers (including the MoE router `ffn_gate_inp`).<br/>
    /// 3. Expert layers (for MoE models), which are used less frequently.<br/>
    /// Within these groups, tensors are prioritized by computational cost (lower-bit quantizations first)
    /// and then by block number.
    /// </summary>
    /// <returns>An ordered IEnumerable of TensorInfo, from most important to least important to offload.</returns>
    public IEnumerable<TensorInfo> GetTensorsForOffload()
    {
        var isMoE = IsMoeModel(this);

        return Tensors
            .OrderBy(t => GetOffloadPriority(t, isMoE))
            .ThenBy(t => GetQuantizationPriority(t.Type)) // REVISED: Use the new priority function
            .ThenBy(t => t.GetBlockNumber())
            .ThenBy(t => t.Name); // Final stable sort
    }

    private static bool IsMoeModel(GgufMetadata metadata)
    {
        if (!metadata.StringValues.TryGetValue("general.architecture", out var arch))
        {
            return false;
        }

        // Check for the expert_count key for the specific architecture
        if (metadata.UInt32Values.TryGetValue($"{arch}.expert_count", out var expertCount))
        {
            return expertCount > 0;
        }

        return false;
    }

    /// <summary>
    /// Assigns a tensor to a priority group for offloading, returning a tuple for multi-level sorting.
    /// Item1 is the major group (foundational, shared, expert).
    /// Item2 is the sub-group (attention vs. FFN).
    /// Lower numbers indicate higher priority.
    /// </summary>
    private static (int Major, int Sub) GetOffloadPriority(TensorInfo tensor, bool isMoE)
    {
        // Sub-priority levels within a major group
        const int attentionSubPriority = 0;
        const int ffnSubPriority = 1;

        // Major Priority 1: Foundational layers
        if (!tensor.Name.StartsWith("blk."))
        {
            return (1, 0);
        }

        // Major Priority 3: Expert layers
        if (isMoE && tensor.Name.Contains("_exp") && !tensor.Name.Contains("ffn_gate_inp"))
        {
            return (3, 0);
        }

        // Major Priority 2: Shared block layers. Now determine sub-priority.
        if (tensor.Name.Contains(".attn_"))
        {
            // Specifically prioritize attention-related layers
            return (2, attentionSubPriority);
        }

        // All other shared block layers (FFN, norms, etc.)
        return (2, ffnSubPriority);
    }

    /// <summary>
    /// Returns a priority score for a tensor type based on its computational complexity for dequantization.
    /// Lower scores indicate higher complexity and thus higher priority for GPU offloading.
    /// </summary>
    private static int GetQuantizationPriority(GgmlType type) => type switch
    {
        // Priority 0: Most complex (1-bit, 2-bit, 3-bit)
        GgmlType.IQ1_S or GgmlType.IQ1_M => 0,
        GgmlType.IQ2_XXS or GgmlType.IQ2_XS or GgmlType.IQ2_S or GgmlType.Q2_K => 1,
        GgmlType.IQ3_XXS or GgmlType.IQ3_S or GgmlType.Q3_K => 2,

        // Priority 1: 4-bit
        GgmlType.Q4_0 or GgmlType.Q4_1 or GgmlType.Q4_K or GgmlType.IQ4_NL or GgmlType.IQ4_XS => 3,

        // Priority 2: 5-bit
        GgmlType.Q5_0 or GgmlType.Q5_1 or GgmlType.Q5_K => 4,

        // Priority 3: 6-bit
        GgmlType.Q6_K => 5,

        // Priority 4: 8-bit
        GgmlType.Q8_0 or GgmlType.Q8_1 or GgmlType.Q8_K or GgmlType.I8 => 6,

        // Priority 5: High precision (less benefit from GPU-specific dequantization kernels)
        GgmlType.I16 => 7,
        GgmlType.F16 => 8,
        GgmlType.I32 => 9,
        GgmlType.F32 => 10,
        GgmlType.I64 => 11,
        GgmlType.F64 => 12,

        // Default for unknown/new types, give them low priority
        _ => 99
    };

    /// <summary>
    /// Calculates the key-value cache size per token based on the GGUF metadata.
    /// </summary>
    /// <remarks>
    /// The size calculation is: hidden layers * kv size * key-value heads * 2 / 1024.<br/>
    /// If kv size isn't present, it's calculated from hidden size * 2 / attention heads instead. That 2 comes from 1 for k + 1 for v.<br/>
    /// The 2 comes from assuming 2-byte precision for both key and value.<br/>
    /// It's in KB for convenience, as the models I checked ranged from 12 KB to 651 KB per token.
    /// <returns>The size of the key-value cache in kilobytes required per token.</returns>
    public int GetKvCacheKBPerToken()
    {
        var architecture = StringValues.GetValueOrDefault("general.architecture", "llama");
        var hiddenLayers = UInt32Values.GetValueOrDefault(architecture + ".block_count", 64u);
        var kvHeads = UInt32Values.GetValueOrDefault(architecture + ".attention.head_count_kv", 8u);
        var kvSize = UInt32Values.GetValueOrDefault(architecture + ".attention.key_length", 0u)
            + UInt32Values.GetValueOrDefault(architecture + ".attention.value_length", 0u);
        if (kvSize == 0)
        {
            kvSize = 2 * UInt32Values.GetValueOrDefault(architecture + ".head_dim", 0u);
        }
        if (kvSize == 0)
        {
            var embeddingLength = UInt32Values.GetValueOrDefault(architecture + ".embedding_length", 0u);
            var attentionHeads = UInt32Values.GetValueOrDefault(architecture + ".attention.head_count", 0u);
            if (embeddingLength > 0 && attentionHeads > 0)
            {
                kvSize = 2 * embeddingLength / attentionHeads;
            }
        }

        return (int)(hiddenLayers * kvHeads * kvSize * 2 / 1024); // * 2 for byte precision
    }

    /// <summary>
    /// Calculates the key-value cache size per token based on provided JSON metadata.<br/>
    /// The kvSize fallback order is:
    /// <list type="number">
    /// <item>qk_nope_head_dim + 2 * qk_rope_head_dim</item>
    /// <item>2 * head_dim</item>
    /// <item>kv_lora_rank + qk_rope_head_dim + v_head_dim</item>
    /// <item>2 * d_kv</item>
    /// <item>2 * hidden_size / num_attention_heads</item>
    /// </list>
    /// </summary>
    /// <remarks><inheritdoc cref="GetKvCacheKBPerToken()"/></remarks>
    /// <param name="json">A JSON string with keys: num_attention_heads, num_hidden_layers, num_key_value_heads.</param>
    /// <returns><inheritdoc cref="GetKvCacheKBPerToken()"/></returns>
    public static int GetKvCacheKBPerToken(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Input JSON cannot be null or empty.", nameof(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Required fields
        if (!root.TryGetProperty("num_hidden_layers", out var hiddenLayersElem) ||
            !root.TryGetProperty("num_key_value_heads", out var kvHeadsElem) ||
            !root.TryGetProperty("num_attention_heads", out var attentionHeadsElem))
        {
            throw new ArgumentException("JSON must contain numeric keys: num_hidden_layers, num_key_value_heads, num_attention_heads.");
        }

        int hiddenLayers = hiddenLayersElem.GetInt32();
        int kvHeads = kvHeadsElem.GetInt32();
        int attentionHeads = attentionHeadsElem.GetInt32();

        // Fallback logic for kvSize
        int? kvSize = null;
        if (root.TryGetProperty("qk_nope_head_dim", out var qkNopeElem) && qkNopeElem.ValueKind == JsonValueKind.Number &&
            root.TryGetProperty("qk_rope_head_dim", out var qkRopeElem) && qkRopeElem.ValueKind == JsonValueKind.Number)
        {
            kvSize = qkNopeElem.GetInt32() + 2 * qkRopeElem.GetInt32();
        }
        else if (root.TryGetProperty("head_dim", out var headDimElem) && headDimElem.ValueKind == JsonValueKind.Number)
        {
            kvSize = 2 * headDimElem.GetInt32();
        }
        else if (root.TryGetProperty("kv_lora_rank", out var kvLoraElem) && kvLoraElem.ValueKind == JsonValueKind.Number &&
                 root.TryGetProperty("qk_rope_head_dim", out var qkRopeElem2) && qkRopeElem2.ValueKind == JsonValueKind.Number &&
                 root.TryGetProperty("v_head_dim", out var vHeadElem) && vHeadElem.ValueKind == JsonValueKind.Number)
        {
            kvSize = kvLoraElem.GetInt32() + qkRopeElem2.GetInt32() + vHeadElem.GetInt32();
        }
        else if (root.TryGetProperty("d_kv", out var dKvElem) && dKvElem.ValueKind == JsonValueKind.Number)
        {
            kvSize = 2 * dKvElem.GetInt32();
        }
        else if (root.TryGetProperty("hidden_size", out var embeddingLenElem) && embeddingLenElem.ValueKind == JsonValueKind.Number &&
                 root.TryGetProperty("num_attention_heads", out var numAttnHeadsElem) && numAttnHeadsElem.ValueKind == JsonValueKind.Number)
        {
            kvSize = (int)(2 * embeddingLenElem.GetInt32() / (double)numAttnHeadsElem.GetInt32());
        }
        else
        {
            throw new ArgumentException("JSON does not contain sufficient information to determine kvSize.");
        }

        // 2 for byte precision (key+value), divide by 1024 for KB
        return (int)Math.Floor(hiddenLayers * kvHeads * kvSize.Value * 2.0 / 1024.0);
    }
}
