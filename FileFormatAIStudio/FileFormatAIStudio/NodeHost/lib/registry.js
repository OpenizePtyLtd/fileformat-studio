const OfficeParserEngine = require('../engines/officeparserEngine');

/**
 * Registry of all available Node.js document parsing engines.
 */
class EngineRegistry {
    constructor() {
        /** @type {Map<string, import('./baseEngine')>} */
        this.engines = new Map();

        // Register built-in engines
        this.register(new OfficeParserEngine());
    }

    /**
     * Registers an engine instance.
     * @param {import('./baseEngine')} engine 
     */
    register(engine) {
        if (!engine || !engine.id) {
            throw new Error('Engine must have an id.');
        }
        this.engines.set(engine.id.toLowerCase(), engine);
    }

    /**
     * Gets an engine by its identifier.
     * @param {string} id 
     * @returns {import('./baseEngine')|undefined}
     */
    get(id) {
        if (!id) return undefined;
        const normalized = id.toLowerCase();
        const direct = this.engines.get(normalized);
        if (direct) return direct;

        // Fallback prefix / alias matching (e.g. 'officeparser-slides' -> 'officeparser')
        if (normalized.startsWith('officeparser')) {
            return this.engines.get('officeparser');
        }

        return undefined;
    }

    /**
     * Finds the best engine for a given extension.
     * @param {string} extension 
     * @returns {import('./baseEngine')|undefined}
     */
    findForExtension(extension) {
        for (const engine of this.engines.values()) {
            if (engine.supportsExtension(extension)) {
                return engine;
            }
        }
        return undefined;
    }

    /**
     * Lists all registered engines.
     * @returns {import('./baseEngine')[]}
     */
    list() {
        return Array.from(this.engines.values());
    }
}

module.exports = new EngineRegistry();

