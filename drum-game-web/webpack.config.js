const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');

module.exports = function (env, argv) {
    const dev = env.WEBPACK_SERVE ?? false;
    const prod = !dev;

    return {
        name: prod ? "production" : "development",
        mode: prod ? "production" : "development",
        entry: './src/index.ts',
        module: {
            rules: [
                {
                    test: /\.tsx?$/,
                    use: 'ts-loader',
                    exclude: /node_modules/,
                },
                {
                    test: /\.s[ac]ss$/i,
                    use: [
                        "style-loader",
                        "css-loader",
                        {
                            loader: "sass-loader",
                            options: {
                                // Prefer `dart-sass`
                                implementation: require("sass"),
                            },
                        },
                    ],
                },
                { test: /\.b?json$/, type: 'json' },
                {
                    test: /\.frag$/i,
                    type: "asset/source"
                }
            ],
        },
        resolve: {
            extensions: ['.ts', '.tsx', '.js'],
        },
        output: {
            filename: 'main.js',
            path: path.resolve(__dirname, 'dist'),
            publicPath: prod ? "/drum-game/" : "/"
        },
        plugins: [new HtmlWebpackPlugin({
            hash: prod,
            template: "./src/index.html",
            filename: prod ? "404.html" : "index.html"
        })],
        devtool: dev ? "inline-source-map" : undefined, // "source-map"
        devServer: dev ? {
            static: './dist',
            headers: {
                "Access-Control-Allow-Origin": "*",
                "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, PATCH, OPTIONS",
                "Access-Control-Allow-Headers": "X-Requested-With, content-type, Authorization"
            },
            historyApiFallback: {
                index: "/"
            }
        } : undefined
    }
}