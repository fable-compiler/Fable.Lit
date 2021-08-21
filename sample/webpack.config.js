// @ts-check
const path = require('path');

const mode = process.env.NODE_ENV || 'development';
const prod = mode === 'production';
console.log(`Bundling for ${mode}...`)

module.exports = {
	entry: {
		bundle: ['./out/App.js']
	},
	output: {
		path: __dirname + '/public',
		filename: '[name].js',
		chunkFilename: '[name].[id].js',
	},
	mode,
	devtool: prod ? false : 'source-map',
    devServer: {
        contentBase: path.join(__dirname, "public"),
        hot: true,
	},
	module: {
		rules: [
			{
				test: /\.css$/i,
				use: ["style-loader", "css-loader", "resolve-url-loader"],
			},
            {
                test: /\.(png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)(\?.*)?$/,
                use: ['file-loader']
            }
		],
	},
};
