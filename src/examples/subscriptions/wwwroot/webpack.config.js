const path = require('path');

module.exports = {
    mode: 'development',
    entry: './src/site.jsx',
    output: {
        filename: 'site.js',
        path: path.resolve(__dirname, 'dist'),
    },
    module: {
        rules: [
            {
                test: /\.(js|jsx)$/,
                exclude: /nodeModules/,
                use: {
                    loader: 'babel-loader'
                }
            }
        ]
  }
};