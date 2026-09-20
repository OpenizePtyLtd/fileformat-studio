const fs = require('fs');
const path = require('path');
const BaseEngine = require('../lib/baseEngine');
const officeParser = require('officeparser');

/**
 * Concrete document parser engine wrapping the 'officeparser' npm package.
 * Supports docx, pptx, xlsx, odt, odp, ods, pdf.
 */
class OfficeParserEngine extends BaseEngine {
    constructor() {
        super(
            'officeparser',
            'officeparser (Node.js)',
            ['.docx', '.pptx', '.xlsx', '.odt', '.odp', '.ods', '.pdf']
        );
    }

    /**
     * Extracts text from the document using officeparser.
     * @param {string} filePath 
     * @param {object} [options] 
     * @returns {Promise<{ text: string, metadata?: object }>}
     */
    async extractText(filePath, options = {}) {
        if (!filePath) {
            throw new Error('File path must be provided.');
        }

        if (!fs.existsSync(filePath)) {
            throw new Error(`File not found: ${filePath}`);
        }

        const ext = path.extname(filePath).toLowerCase();
        if (!this.supportsExtension(ext)) {
            throw new Error(`officeparser does not support extension '${ext}'.`);
        }

        // Default config: disable OCR by default for maximum speed unless explicitly requested
        const config = {
            newlineDelimiter: '\n',
            ignoreNotes: false,
            ...options
        };

        const parsed = await officeParser.parseOffice(filePath, config);

        let extractedText = '';
        if (typeof parsed === 'string') {
            extractedText = parsed;
        } else if (parsed && typeof parsed.toText === 'function') {
            extractedText = parsed.toText();
        } else if (parsed && typeof parsed.text === 'string') {
            extractedText = parsed.text;
        } else {
            extractedText = String(parsed || '');
        }

        return {
            text: extractedText,
            metadata: {
                engine: this.id,
                fileExtension: ext,
                fileSizeBytes: fs.statSync(filePath).size
            }
        };
    }

    async extractMetadata(filePath) {
        if (!fs.existsSync(filePath)) return {};
        const stats = fs.statSync(filePath);
        return {
            fileSizeBytes: stats.size,
            lastModified: stats.mtime.toISOString(),
            extension: path.extname(filePath).toLowerCase()
        };
    }
}

module.exports = OfficeParserEngine;

