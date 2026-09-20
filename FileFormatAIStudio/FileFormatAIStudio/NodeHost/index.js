const fs = require('fs');
const readline = require('readline');
const registry = require('./lib/registry');
const { createSuccessResponse, createErrorResponse, createProbeResponse } = require('./lib/protocol');

/**
 * Handles a single parsed request object.
 * @param {object} req 
 * @returns {Promise<object>}
 */
async function handleRequest(req) {
    const startTime = process.hrtime.bigint();
    const requestId = req.requestId || null;

    try {
        if (!req || typeof req !== 'object') {
            throw new Error('Invalid request payload. Must be a JSON object.');
        }

        const command = (req.command || 'extractText').toLowerCase();

        // Handle diagnostic / probe commands
        if (command === 'probe' || command === 'listengines') {
            return createProbeResponse(requestId, registry.list());
        }

        const filePath = req.filePath;
        if (!filePath) {
            throw new Error('filePath is required in request.');
        }

        let engineId = req.engineId;
        let engine;

        if (engineId) {
            engine = registry.get(engineId);
            if (!engine) {
                throw new Error(`Engine '${engineId}' is not registered in NodeHost.`);
            }
        } else {
            // Auto-resolve by extension
            const ext = filePath.includes('.') ? '.' + filePath.split('.').pop() : '';
            engine = registry.findForExtension(ext);
            if (!engine) {
                throw new Error(`No NodeHost engine registered for extension '${ext}'.`);
            }
        }

        if (command === 'extracttext') {
            const result = await engine.extractText(filePath, req.options || {});
            const elapsedMs = Number(process.hrtime.bigint() - startTime) / 1_000_000;
            return createSuccessResponse(requestId, result.text, result.metadata, elapsedMs);
        } else if (command === 'extractmetadata') {
            const metadata = await engine.extractMetadata(filePath);
            const elapsedMs = Number(process.hrtime.bigint() - startTime) / 1_000_000;
            return createSuccessResponse(requestId, '', metadata, elapsedMs);
        } else {
            throw new Error(`Unknown command: '${req.command}'.`);
        }
    } catch (err) {
        const elapsedMs = Number(process.hrtime.bigint() - startTime) / 1_000_000;
        return createErrorResponse(requestId, err, elapsedMs);
    }
}

/**
 * CLI Entrypoint
 */
async function main() {
    const args = process.argv.slice(2);

    // Global uncaught exception and rejection handlers
    process.on('uncaughtException', (err) => {
        const res = createErrorResponse(null, err);
        process.stdout.write(JSON.stringify(res) + '\n');
        process.exit(1);
    });

    process.on('unhandledRejection', (reason) => {
        const res = createErrorResponse(null, reason);
        process.stdout.write(JSON.stringify(res) + '\n');
        process.exit(1);
    });

    // 1. Probe flag
    if (args.includes('--probe')) {
        const res = createProbeResponse(null, registry.list());
        process.stdout.write(JSON.stringify(res) + '\n');
        process.exit(0);
    }

    // 2. Request from Base64 string
    const requestIndex = args.indexOf('--request');
    if (requestIndex !== -1 && args[requestIndex + 1]) {
        try {
            const rawBase64 = args[requestIndex + 1];
            const jsonStr = Buffer.from(rawBase64, 'base64').toString('utf8');
            const req = JSON.parse(jsonStr);
            const response = await handleRequest(req);
            process.stdout.write(JSON.stringify(response) + '\n');
            process.exit(response.success ? 0 : 1);
        } catch (err) {
            const res = createErrorResponse(null, err);
            process.stdout.write(JSON.stringify(res) + '\n');
            process.exit(1);
        }
        return;
    }

    // 3. Request from File
    const fileIndex = args.indexOf('--request-file');
    if (fileIndex !== -1 && args[fileIndex + 1]) {
        try {
            const reqPath = args[fileIndex + 1];
            const jsonStr = fs.readFileSync(reqPath, 'utf8');
            const req = JSON.parse(jsonStr);
            const response = await handleRequest(req);
            process.stdout.write(JSON.stringify(response) + '\n');
            process.exit(response.success ? 0 : 1);
        } catch (err) {
            const res = createErrorResponse(null, err);
            process.stdout.write(JSON.stringify(res) + '\n');
            process.exit(1);
        }
        return;
    }

    // 4. Stdin / Interactive mode (NDJSON)
    if (args.includes('--interactive') || !process.stdin.isTTY) {
        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout,
            terminal: false
        });

        rl.on('line', async (line) => {
            const trimmed = line.trim();
            if (!trimmed) return;

            if (trimmed === 'exit' || trimmed === 'quit') {
                process.exit(0);
            }

            try {
                const req = JSON.parse(trimmed);
                const response = await handleRequest(req);
                process.stdout.write(JSON.stringify(response) + '\n');
            } catch (err) {
                const res = createErrorResponse(null, err);
                process.stdout.write(JSON.stringify(res) + '\n');
            }
        });

        rl.on('close', () => {
            process.exit(0);
        });

        return;
    }

    // Default usage
    console.error('Usage: node index.js [--probe | --request <base64> | --request-file <path> | --interactive]');
    process.exit(1);
}

main();

