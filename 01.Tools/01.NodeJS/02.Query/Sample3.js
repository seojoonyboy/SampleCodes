process.env.TZ = 'Asia/Seoul';

const mysql = require('mysql2/promise');
const CONFIG = require('./config.js');

async function fetchRow(appName, groupID) {

    const CFG = CONFIG[appName];
    const con = await mysql.createConnection({
        host: CFG.DB.host,
        port: CFG.DB.port,
        user: CFG.DB.user,
        password: CFG.DB.password, 
    });
    await con.connect();

    await con.execute(`SET @groupID = ${groupID}`);

    // CardData.percentage = 1 → 일반테마
    const [r] = await con.execute(`
        SELECT
            A.theme,
            SUM(A.cnt) AS cnt
        FROM (
            SELECT
                CD.theme,
                COUNT(*) AS cnt
            FROM
                ${CFG.DB.account_db}1.Card C
            INNER JOIN ${CFG.DB.master_db}.CardData CD ON C.cardID = CD.code AND CD.percentage = 1
            WHERE
                C.groupID = @groupID
            GROUP BY
                CD.theme

            UNION ALL

            SELECT
                CD.theme,
                COUNT(*) AS cnt
            FROM
                ${CFG.DB.account_db}2.Card C
            INNER JOIN ${CFG.DB.master_db}.CardData CD ON C.cardID = CD.code AND CD.percentage = 1
            WHERE
                C.groupID = @groupID
            GROUP BY
                CD.theme
        ) A
        GROUP BY
            A.theme
    `);

    await con.end();

    return r;
}


const GROUPIDs = {
    SSM: [
        1,2,3,5,6,      7,8,9,10,11, 
        12,16,19,21,22, 23,29,31,32,33,
        28,35,36,37,42, 43,45,46,50,52,
        5500,6000,7000,8000,8500, 9000,13000,15000,16000,19000,
        23000
    ],
    SSJ: [ 10, 11, 31, 53 ],
};

async function main() {
    const appNames = Object.keys(GROUPIDs);
    for (const appName of appNames) {
        const cs = GROUPIDs[appName];
        for (const groupID of cs) {
            // console.time(`${appName}, ${groupID}`);
            const rows = await fetchRow(appName, groupID);
            // console.timeEnd(`${appName}, ${groupID}`);
            let line = `${appName}\t${groupID}`;
            for (const r of rows) {
                line += `\t${r.theme}\t${r.cnt}`
            }
            console.log(line);
        }
    };
}

main();

