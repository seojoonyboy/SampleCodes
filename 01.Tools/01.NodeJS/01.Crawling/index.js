exports.handler = async () => {
    await require('./app')();

    return {
        statusCode: 200,
        body: "ok"
    };
};