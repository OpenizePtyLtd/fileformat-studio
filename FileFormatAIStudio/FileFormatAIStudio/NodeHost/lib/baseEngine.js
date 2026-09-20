/**
 * Base abstract class for all document parser engines running within NodeHost.
 * Concrete engines (e.g. officeparser, mammoth, xlsx, pdf-parse) extend this class.
 */
class BaseEngine {
    /**
     * @param {string} id - Unique engine identifier (e.g. 'officeparser', 'mammoth')
     * @param {string} name - Friendly display name
     * @param {string[]} supportedExtensions - Normalized lowercase file extensions (e.g. ['.docx', '.xlsx'])
     */
    constructor(id, name, supportedExtensions) {
        if (!id) throw new Error("Engine ID is required.");
        if (!name) throw new Error("Engine Name is required.");
        if (!Array.isArray(supportedExtensions)) throw new Error("supportedExtensions must be an array.");

        this.id = id.toLowerCase();
        this.name = name;
        this.supportedExtensions = supportedExtensions.map(ext => ext.startsWith('.') ? ext.toLowerCase() : '.' + ext.toLowerCase());
    }

    /**
     * Determines whether this engine can parse the given file extension.
     * @param {string} extension 
     * @returns {boolean}
     */
    supportsExtension(extension) {
        if (!extension) return false;
        const normalized = extension.startsWith('.') ? extension.toLowerCase() : '.' + extension.toLowerCase();
        return this.supportedExtensions.includes(normalized);
    }

    /**
     * Extracts raw text from the specified document file.
     * @param {string} filePath - Absolute path to the file.
     * @param {object} [options] - Optional parsing configurations.
     * @returns {Promise<{ text: string, metadata?: object }>}
     */
    async extractText(filePath, options = {}) {
        throw new Error(`extractText() is not implemented for engine '${this.id}'.`);
    }

    /**
     * Extracts metadata from the specified document file.
     * @param {string} filePath - Absolute path to the file.
     * @returns {Promise<object>}
     */
    async extractMetadata(filePath) {
        return {};
    }
}

module.exports = BaseEngine;

