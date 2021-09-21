// This file is used to minify and tree-shake the bits
// of JS needed and include them in the Nuget package

module.exports = {
    entry: './queries.js',
    mode: "production",
    experiments: {
        outputModule: true,
    },
    output: {
        filename: "queries.min.js",
        path: __dirname,
        library: {
            type: 'module',
        },
    },
};