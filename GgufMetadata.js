/**
 * Loads metadata (excluding arrays) and tensor metadata from a GGUF v3 file according to the specification at https://github.com/ggerganov/ggml/blob/master/docs/gguf.md
 */
class GgufMetadata {
    constructor() {
        // Define objects for each datatype in the file specification, to maintain strong typing
        this.uint8Values = {};
        this.int8Values = {};
        this.uint16Values = {};
        this.int16Values = {};
        this.uint32Values = {};
        this.int32Values = {};
        this.float32Values = {};
        this.boolValues = {};
        this.stringValues = {};
        this.uint64Values = {};
        this.int64Values = {};
        this.float64Values = {};

        /**
         * For use at design-time to help you figure out the appropriate types for the values you're seeking.
         */
        this.valueTypes = {};

        this.tensors = [];
        
        this.chunk = null;
        this.view = null;
        this.lastWaitTime = Date.now();
    }

    /**
     * Loads GGUF metadata from a File object
     * @param {File} file - The GGUF file to parse
     */
    async loadFromFile(file, waiter) {
        // Read a much larger initial chunk - most GGUF headers are under 1MB
        // but let's be generous and read 5MB to avoid most re-reads
        const initialChunkSize = Math.min(5 * 1024 * 1024, file.size); // 5MB or file size, whichever is smaller
        this.chunk = await this._readFileSlice(file, 0, initialChunkSize);
        this.view = new DataView(this.chunk);
        let offset = 0;

        // Check magic number
        const magic = this.view.getUint32(offset, true); // little-endian
        if (magic !== 0x46554747) { // 'GGUF' in little-endian
            throw new Error("Not a GGUF file.");
        }
        offset += 4;

        const version = this.view.getInt32(offset, true);
        if (version !== 3) {
            throw new Error(`Unsupported GGUF version ${version}. This class only supports v3.`);
        }
        offset += 4;

        const tensorCount = this._readUint64(offset);
        offset += 8;

        const metadataCount = this._readUint64(offset);
        offset += 8;

        // Read metadata
        for (let x = 0; x < Number(metadataCount); x++) {
            await this._ensureDataAvailable(file, offset, 1024); // Ensure more generous buffer
            offset = await this._readMetadataKeyValuePair(file, offset);
            await this._maybeWait();
        }

        // Read tensor metadata
        this.tensors = [];
        for (let i = 0; i < Number(tensorCount); i++) {
            await this._ensureDataAvailable(file, offset, 1024); // Ensure space for tensor metadata
            
            const tensorResult = await this._readTensorInfo(file, offset);
            this.tensors.push(tensorResult.tensor);
            offset = tensorResult.offset;
            await this._maybeWait();
        }
    }

    async _maybeWait() {
        if (typeof waiter === 'function' && Date.now() - this.lastWaitTime >= 100) {
            await waiter();
            this.lastWaitTime = Date.now();
        }
    }

    async _readFileSlice(file, start, length) {
        const slice = file.slice(start, start + length);
        return await slice.arrayBuffer();
    }

    async _ensureDataAvailable(file, offset, minBytesNeeded) {
        if (offset + minBytesNeeded <= this.chunk.byteLength) {
            return;
        }

        // Need to read more data - double the size or add 1MB, whichever is larger
        const currentSize = this.chunk.byteLength;
        const neededSize = offset + minBytesNeeded;
        const newSize = Math.max(currentSize * 2, neededSize + 1024 * 1024); // At least 1MB extra buffer
        const finalSize = Math.min(newSize, file.size);
        
        // Only re-read if we actually need more data
        if (finalSize > currentSize) {
            this.chunk = await this._readFileSlice(file, 0, finalSize);
            this.view = new DataView(this.chunk);
            return;
        }
    }

    _readUint64(offset) {
        return this.view.getBigUint64(offset, true);
    }

    _readInt64(offset) {
        return this.view.getBigInt64(offset, true);
    }

    async _readString(file, offset) {
        const length = this._readUint64(offset);
        offset += 8;

        // Convert BigInt to number for byte operations
        const lengthNum = Number(length);
        if (lengthNum > Number.MAX_SAFE_INTEGER) {
            throw new Error("String length too large for JavaScript");
        }

        // Ensure we have enough data for the string
        if (offset + lengthNum > this.chunk.byteLength) {
            await this._ensureDataAvailable(file, offset, lengthNum);
        }

        const bytes = new Uint8Array(this.chunk, offset, lengthNum);
        const string = new TextDecoder('utf-8').decode(bytes);
        return { string, newOffset: offset + lengthNum };
    }

    async _readMetadataKeyValuePair(file, offset) {
        // Read key string
        const keyLength = this._readUint64(offset);
        offset += 8;

        const keyLengthNum = Number(keyLength);
        if (keyLengthNum > Number.MAX_SAFE_INTEGER) {
            throw new Error("Key length too large for JavaScript");
        }

        // Ensure we have enough data for the key
        if (offset + keyLengthNum > this.chunk.byteLength) {
            await this._ensureDataAvailable(file, offset, keyLengthNum + 32); // Extra buffer for value type
        }

        const keyBytes = new Uint8Array(this.chunk, offset, keyLengthNum);
        const key = new TextDecoder('utf-8').decode(keyBytes);
        offset += keyLengthNum;

        // Ensure we have the value type
        if (offset + 4 > this.chunk.byteLength) {
            await this._ensureDataAvailable(file, offset, 32);
        }

        const valueType = this.view.getUint32(offset, true);
        offset += 4;

        switch (valueType) {
            case GgufMetadataValueType.UINT8:
                this.uint8Values[key] = this.view.getUint8(offset);
                this.valueTypes[key] = 'uint8';
                offset += 1;
                break;
            case GgufMetadataValueType.INT8:
                this.int8Values[key] = this.view.getInt8(offset);
                this.valueTypes[key] = 'int8';
                offset += 1;
                break;
            case GgufMetadataValueType.UINT16:
                this.uint16Values[key] = this.view.getUint16(offset, true);
                this.valueTypes[key] = 'uint16';
                offset += 2;
                break;
            case GgufMetadataValueType.INT16:
                this.int16Values[key] = this.view.getInt16(offset, true);
                this.valueTypes[key] = 'int16';
                offset += 2;
                break;
            case GgufMetadataValueType.UINT32:
                this.uint32Values[key] = this.view.getUint32(offset, true);
                this.valueTypes[key] = 'uint32';
                offset += 4;
                break;
            case GgufMetadataValueType.INT32:
                this.int32Values[key] = this.view.getInt32(offset, true);
                this.valueTypes[key] = 'int32';
                offset += 4;
                break;
            case GgufMetadataValueType.FLOAT32:
                this.float32Values[key] = this.view.getFloat32(offset, true);
                this.valueTypes[key] = 'float32';
                offset += 4;
                break;
            case GgufMetadataValueType.BOOL:
                this.boolValues[key] = this.view.getUint8(offset) !== 0;
                this.valueTypes[key] = 'bool';
                offset += 1;
                break;
            case GgufMetadataValueType.STRING:
                const stringResult = await this._readString(file, offset);
                this.stringValues[key] = stringResult.string;
                this.valueTypes[key] = 'string';
                offset = stringResult.newOffset;
                break;
            case GgufMetadataValueType.UINT64:
                this.uint64Values[key] = this._readUint64(offset);
                this.valueTypes[key] = 'uint64';
                offset += 8;
                break;
            case GgufMetadataValueType.INT64:
                this.int64Values[key] = this._readInt64(offset);
                this.valueTypes[key] = 'int64';
                offset += 8;
                break;
            case GgufMetadataValueType.FLOAT64:
                this.float64Values[key] = this.view.getFloat64(offset, true);
                this.valueTypes[key] = 'float64';
                offset += 8;
                break;
            case GgufMetadataValueType.ARRAY:
                // Skip arrays entirely.
                offset = await this._skipArray(file, offset);
                break;
            default:
                throw new Error(`Unknown metadata value type: ${valueType}`);
        }

        return offset;
    }

    // Separate method for reading string values to avoid confusion with key reading
    async _readStringValue(file, offset) {
        const length = this._readUint64(offset);
        offset += 8;

        const lengthNum = Number(length);
        if (lengthNum > Number.MAX_SAFE_INTEGER) {
            throw new Error("String length too large for JavaScript");
        }

        // Ensure we have enough data for the string
        if (offset + lengthNum > this.chunk.byteLength) {
            await this._ensureDataAvailable(file, offset, lengthNum);
        }

        const bytes = new Uint8Array(this.chunk, offset, lengthNum);
        const string = new TextDecoder('utf-8').decode(bytes);
        return { string, newOffset: offset + lengthNum };
    }

    async _readTensorInfo(file, offset) {
        const nameResult = await this._readString(file, offset);
        const name = nameResult.string;
        offset = nameResult.newOffset;

        // Ensure we have enough data for tensor metadata
        const result = await this._ensureDataAvailable(file, offset, 64);

        const nDimensions = this.view.getUint32(offset, true);
        offset += 4;

        const dimensions = [];
        for (let d = 0; d < nDimensions; d++) {
            dimensions.push(this._readUint64(offset));
            offset += 8;
        }

        const type = this.view.getUint32(offset, true);
        offset += 4;

        const tensorOffset = this._readUint64(offset);
        offset += 8;

        const tensor = new TensorInfo(name, type, dimensions, tensorOffset);
        return { tensor, offset };
    }

    async _skipArray(file, offset) {
        // Ensure we have data for array header
        await this._ensureDataAvailable(file, offset, 12);

        const elementType = this.view.getUint32(offset, true);
        offset += 4;
        const length = this._readUint64(offset);
        offset += 8;

        // Convert BigInt to number for loop
        const lengthNum = Number(length);
        if (lengthNum > Number.MAX_SAFE_INTEGER) {
            throw new Error("Array length too large for JavaScript");
        }

        for (let i = 0; i < lengthNum; i++) {
            offset = await this._skipValue(file, offset, elementType);
        }
        return offset;
    }

    async _skipValue(file, offset, valueType) {
        // Ensure we have enough data
        await this._ensureDataAvailable(file, offset, 16);

        switch (valueType) {
            case GgufMetadataValueType.UINT8:
            case GgufMetadataValueType.INT8:
            case GgufMetadataValueType.BOOL:
                offset += 1;
                break;
            case GgufMetadataValueType.UINT16:
            case GgufMetadataValueType.INT16:
                offset += 2;
                break;
            case GgufMetadataValueType.UINT32:
            case GgufMetadataValueType.INT32:
            case GgufMetadataValueType.FLOAT32:
                offset += 4;
                break;
            case GgufMetadataValueType.UINT64:
            case GgufMetadataValueType.INT64:
            case GgufMetadataValueType.FLOAT64:
                offset += 8;
                break;
            case GgufMetadataValueType.STRING:
                const stringResult = await this._readString(file, offset);
                offset = stringResult.newOffset;
                break;
            case GgufMetadataValueType.ARRAY:
                offset = await this._skipArray(file, offset);
                break;
            default:
                throw new Error(`Unknown metadata value type: ${valueType}`);
        }
        return offset;
    }

    /**
     * Gets the list of tensors sorted in a hopefully optimal order for offloading to a GPU with limited VRAM.
     * The priority is:
     * 1. Foundational shared layers (embeddings, output norm, etc.).
     * 2. Standard transformer block shared layers (including the MoE router `ffn_gate_inp`).
     * 3. Expert layers (for MoE models), which are used less frequently.
     * Within these groups, tensors are prioritized by computational cost (lower-bit quantizations first)
     * and then by block number.
     * @returns {TensorInfo[]} An ordered array of TensorInfo, from most important to least important to offload.
     */
    getTensorsForOffload() {
        const isMoE = this._isMoeModel();

        return this.tensors
            .slice() // Create a copy to avoid mutating the original array
            .sort((a, b) => {
                const aPriority = this._getOffloadPriority(a, isMoE);
                const bPriority = this._getOffloadPriority(b, isMoE);
                
                // Compare major priority first
                if (aPriority.major !== bPriority.major) {
                    return aPriority.major - bPriority.major;
                }
                
                // Then sub priority
                if (aPriority.sub !== bPriority.sub) {
                    return aPriority.sub - bPriority.sub;
                }
                
                // Then quantization priority
                const aQuantPriority = this._getQuantizationPriority(a.type);
                const bQuantPriority = this._getQuantizationPriority(b.type);
                if (aQuantPriority !== bQuantPriority) {
                    return aQuantPriority - bQuantPriority;
                }
                
                // Then block number
                const aBlockNum = a.getBlockNumber();
                const bBlockNum = b.getBlockNumber();
                if (aBlockNum !== bBlockNum) {
                    return aBlockNum - bBlockNum;
                }
                
                // Finally, name for stable sort
                return a.name.localeCompare(b.name);
            });
    }

    _isMoeModel() {
        const arch = this.stringValues['general.architecture'];
        if (!arch) {
            return false;
        }

        // Check for the expert_count key for the specific architecture
        const expertCount = this.uint32Values[`${arch}.expert_count`];
        return expertCount && expertCount > 0;
    }

    /**
     * Assigns a tensor to a priority group for offloading, returning an object for multi-level sorting.
     * major is the major group (foundational, shared, expert).
     * sub is the sub-group (attention vs. FFN).
     * Lower numbers indicate higher priority.
     */
    _getOffloadPriority(tensor, isMoE) {
        // Sub-priority levels within a major group
        const attentionSubPriority = 0;
        const ffnSubPriority = 1;

        // Major Priority 1: Foundational layers
        if (!tensor.name.startsWith("blk.")) {
            return { major: 1, sub: 0 };
        }

        // Major Priority 3: Expert layers
        if (isMoE && tensor.name.includes("_exp") && !tensor.name.includes("ffn_gate_inp")) {
            return { major: 3, sub: 0 };
        }

        // Major Priority 2: Shared block layers. Now determine sub-priority.
        if (tensor.name.includes(".attn_")) {
            // Specifically prioritize attention-related layers
            return { major: 2, sub: attentionSubPriority };
        }

        // All other shared block layers (FFN, norms, etc.)
        return { major: 2, sub: ffnSubPriority };
    }

    /**
     * Returns a priority score for a tensor type based on its computational complexity for dequantization.
     * Lower scores indicate higher complexity and thus higher priority for GPU offloading.
     */
    _getQuantizationPriority(type) {
        switch (type) {
            // Priority 0: Most complex (1-bit, 2-bit, 3-bit)
            case GgmlType.IQ1_S:
            case GgmlType.IQ1_M:
                return 0;
            case GgmlType.IQ2_XXS:
            case GgmlType.IQ2_XS:
            case GgmlType.IQ2_S:
            case GgmlType.Q2_K:
                return 1;
            case GgmlType.IQ3_XXS:
            case GgmlType.IQ3_S:
            case GgmlType.Q3_K:
                return 2;

            // Priority 1: 4-bit
            case GgmlType.Q4_0:
            case GgmlType.Q4_1:
            case GgmlType.Q4_K:
            case GgmlType.IQ4_NL:
            case GgmlType.IQ4_XS:
                return 3;

            // Priority 2: 5-bit
            case GgmlType.Q5_0:
            case GgmlType.Q5_1:
            case GgmlType.Q5_K:
                return 4;

            // Priority 3: 6-bit
            case GgmlType.Q6_K:
                return 5;

            // Priority 4: 8-bit
            case GgmlType.Q8_0:
            case GgmlType.Q8_1:
            case GgmlType.Q8_K:
            case GgmlType.I8:
                return 6;

            // Priority 5: High precision (less benefit from GPU-specific dequantization kernels)
            case GgmlType.I16:
                return 7;
            case GgmlType.F16:
                return 8;
            case GgmlType.I32:
                return 9;
            case GgmlType.F32:
                return 10;
            case GgmlType.I64:
                return 11;
            case GgmlType.F64:
                return 12;

            // Default for unknown/new types, give them low priority
            default:
                return 99;
        }
    }

    /**
     * Calculates the key-value cache size per token based on the GGUF metadata.
     * The size calculation is: hidden layers * hidden size * key-value heads * 4 / attention heads / 1024.
     * The 4 comes from assuming 2-byte precision for both key and value.
     * It's in KB for convenience, as the models I checked ranged from 12 KB to 651 KB per token.
     * @returns {number} The size of the key-value cache in kilobytes required per token.
     */
    getKvCacheKBPerToken() {
        const architecture = this.stringValues['general.architecture'] || 'llama';
        const hiddenLayers = this.uint32Values[architecture + '.block_count'] || 64;
        const hiddenSize = this.uint32Values[architecture + '.embedding_length'] || 5120;
        const attentionHeads = this.uint32Values[architecture + '.attention.head_count'] || 40;
        const kvHeads = this.uint32Values[architecture + '.attention.head_count_kv'] || 8;
        return Math.floor(hiddenLayers * hiddenSize * kvHeads * 4 / attentionHeads / 1024); // 2 for key and value, 2 for byte precision
    }
}

const GgufMetadataValueType = {
    UINT8: 0,
    INT8: 1,
    UINT16: 2,
    INT16: 3,
    UINT32: 4,
    INT32: 5,
    FLOAT32: 6,
    BOOL: 7,
    STRING: 8,
    ARRAY: 9,
    UINT64: 10,
    INT64: 11,
    FLOAT64: 12
};

const GgmlType = {
    F32: 0,
    F16: 1,
    Q4_0: 2,
    Q4_1: 3,
    // Q4_2/3 removed
    Q5_0: 6,
    Q5_1: 7,
    Q8_0: 8,
    Q8_1: 9,
    Q2_K: 10,
    Q3_K: 11,
    Q4_K: 12,
    Q5_K: 13,
    Q6_K: 14,
    Q8_K: 15,
    IQ2_XXS: 16,
    IQ2_XS: 17,
    IQ3_XXS: 18,
    IQ1_S: 19,
    IQ4_NL: 20,
    IQ3_S: 21,
    IQ2_S: 22,
    IQ4_XS: 23,
    I8: 24,
    I16: 25,
    I32: 26,
    I64: 27,
    F64: 28,
    IQ1_M: 29,
};

/**
 * Represents metadata information about a tensor, including its name, type, dimensions, and offset.
 */
class TensorInfo {
    constructor(name, type, dimensions, offset) {
        this.name = name;
        this.type = type;
        this.dimensions = dimensions;
        this.offset = offset;
    }

    /**
     * Extracts the block number from the name property, e.g., from "blk.12.attn_norm.weight" -> 12
     * The method expects the name to start with "blk." followed by the block
     * number. If the format is not followed, or the block number cannot be parsed, the method returns
     * -1.
     * @returns {number} The block number extracted from the name if the format is correct; otherwise, -1.
     */
    getBlockNumber() {
        if (!this.name.startsWith("blk.")) return -1;
        const parts = this.name.split('.');
        if (parts.length > 1) {
            const blockNum = parseInt(parts[1], 10);
            if (!isNaN(blockNum)) {
                return blockNum;
            }
        }
        return -1;
    }
}
