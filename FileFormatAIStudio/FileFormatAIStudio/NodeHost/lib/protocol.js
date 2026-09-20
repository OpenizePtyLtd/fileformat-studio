/**
 * IPC protocol envelope serialization and response factories.
 */

function createSuccessResponse(requestId, text, metadata = {}, executionMs = 0) {
    const textStr = typeof text === 'string' ? text : '';
    return {
        requestId: requestId || null,
        success: true,
        text: textStr,
        characterCount: textStr.length,
        executionMs: Math.round(executionMs),
        metadata: metadata || {},
        error: null
    };
}

function createErrorResponse(requestId, error, executionMs = 0) {
    const errorMessage = error instanceof Error 
        ? `${error.name}: ${error.message}\n${error.stack || ''}` 
        : String(error || 'Unknown error');

    return {
        requestId: requestId || null,
        success: false,
        text: null,
        characterCount: 0,
        executionMs: Math.round(executionMs),
        metadata: {},
        error: errorMessage.trim()
    };
}

function createProbeResponse(requestId, engines, runtimeInfo = {}) {
    return {
        requestId: requestId || null,
        success: true,
        text: null,
        characterCount: 0,
        executionMs: 0,
        metadata: {
            isProbe: true,
            nodeVersion: process.version,
            platform: process.platform,
            arch: process.arch,
            pid: process.pid,
            engines: engines.map(e => ({
                id: e.id,
                name: e.name,
                supportedExtensions: e.supportedExtensions
            })),
            ...runtimeInfo
        },
        error: null
    };
}

module.exports = {
    createSuccessResponse,
    createErrorResponse,
    createProbeResponse
};

