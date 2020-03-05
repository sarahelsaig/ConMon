const VueLoaderPlugin = require('vue-loader/lib/plugin');

module.exports = {
    "mode": "development",
    "entry": __dirname + "/Scripts/index.js",
    "output": {
        "path": __dirname + '/wwwroot',
        "filename": "site.bottom.js"
    },
    "module": {
        "rules": [
            {
                "test": /\.vue$/,
                "exclude": /node_modules/,
                "use": "vue-loader"
            },
            {
                "test": /\.less$/,
                "use": [
                    "style-loader",
                    "css-loader",
                    "less-loader"
                ]
            }
        ]
    },
    plugins: [
        new VueLoaderPlugin()
    ]
};