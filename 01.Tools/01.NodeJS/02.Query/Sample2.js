process.env.TZ = 'Asia/Seoul';

const mysql = require('mysql2/promise');
const CONFIG = require('./config.js');

async function fetchRow(appName, groupID, theme, gcount) {

    const CFG = CONFIG[appName];
    const con = await mysql.createConnection({
        host: CFG.DB.host,
        port: CFG.DB.port,
        user: CFG.DB.user,
        password: CFG.DB.password, 
    });
    await con.connect();

    await con.execute(`SET @groupID = ${groupID}`);
    await con.execute(`SET @theme = ${theme}`);
    await con.execute(`SET @gcount = ${gcount}`);

    const [r] = await con.execute(`
        SELECT
	        COUNT(*) AS CNT
        FROM (
            SELECT
                owner,
                COUNT(DISTINCT(cardID)) AS CNT
            FROM
                ${CFG.DB.account_db}1.Card C
            INNER JOIN ${CFG.DB.master_db}.CardData CD ON C.cardID = CD.code AND CD.percentage = 1
            WHERE
                C.groupID = @groupID
                AND CD.theme = @theme
                AND C.owner IN (SELECT objectID FROM ${CFG.DB.account_db}1.User U WHERE U.lastLogin >= '2021-03-12 00:00:00')
            GROUP BY
                owner
            HAVING
                CNT >= @gcount
                
            UNION ALL
            
            SELECT
                owner,
                COUNT(DISTINCT(cardID)) AS CNT
            FROM
            ${CFG.DB.account_db}2.Card C
            INNER JOIN ${CFG.DB.master_db}.CardData CD ON C.cardID = CD.code AND CD.percentage = 1
            WHERE
                C.groupID = @groupID
                AND CD.theme = @theme
                AND C.owner IN (SELECT objectID FROM ${CFG.DB.account_db}2.User U WHERE U.lastLogin >= '2021-03-12 00:00:00')
            GROUP BY
                owner
            HAVING
                CNT >= @gcount
        ) A
    `);
    const result = r[0].CNT;

    await con.end();

    return result;
}


const CHUNKS = {
    /*
    SSJ: [
        // TWICE
        { groupID: 10, theme: 1001, gcount: 9 },
        { groupID: 10, theme: 1002, gcount: 9 },
        { groupID: 10, theme: 1003, gcount: 9 },
        { groupID: 10, theme: 1007, gcount: 9 },
        { groupID: 10, theme: 1008, gcount: 9 },
        { groupID: 10, theme: 1010, gcount: 9 },
        { groupID: 10, theme: 1012, gcount: 9 },
        { groupID: 10, theme: 1014, gcount: 9 },
        { groupID: 10, theme: 1016, gcount: 9 },
        { groupID: 10, theme: 1018, gcount: 9 },
        { groupID: 10, theme: 1020, gcount: 9 },
        { groupID: 10, theme: 1023, gcount: 9 },
        { groupID: 10, theme: 1026, gcount: 9 },
        { groupID: 10, theme: 1028, gcount: 9 },
        { groupID: 10, theme: 1031, gcount: 9 },
        // DAY6
        { groupID: 11, theme: 1101, gcount: 5 },
        { groupID: 11, theme: 1102, gcount: 5 },
        { groupID: 11, theme: 1103, gcount: 5 },
        { groupID: 11, theme: 1105, gcount: 5 },
        { groupID: 11, theme: 1106, gcount: 5 },
        { groupID: 11, theme: 1107, gcount: 5 },
        { groupID: 11, theme: 1108, gcount: 5 },
        { groupID: 11, theme: 1110, gcount: 5 },
        { groupID: 11, theme: 1112, gcount: 5 },
        { groupID: 11, theme: 1113, gcount: 5 },
        { groupID: 11, theme: 1115, gcount: 5 },
        { groupID: 11, theme: 1116, gcount: 5 },
        { groupID: 11, theme: 1118, gcount: 5 },
        // Stray Kids
        { groupID: 31, theme: 3101, gcount: 8 },
        { groupID: 31, theme: 3102, gcount: 8 },
        { groupID: 31, theme: 3104, gcount: 8 },
        { groupID: 31, theme: 3105, gcount: 8 },
        { groupID: 31, theme: 3106, gcount: 8 },
        { groupID: 31, theme: 3107, gcount: 8 },
        { groupID: 31, theme: 3108, gcount: 8 },
        { groupID: 31, theme: 3109, gcount: 8 },
        { groupID: 31, theme: 3111, gcount: 8 },
        { groupID: 31, theme: 3112, gcount: 8 },
        { groupID: 31, theme: 3113, gcount: 8 },
        { groupID: 31, theme: 3116, gcount: 8 },
        { groupID: 31, theme: 3119, gcount: 8 },
        // ITZY
        { groupID: 53, theme: 8101, gcount: 5 },
        { groupID: 53, theme: 8102, gcount: 5 },
        { groupID: 53, theme: 8110, gcount: 5 },
        { groupID: 53, theme: 8112, gcount: 5 },
        { groupID: 53, theme: 8116, gcount: 5 },
    ],
    */
    SSM: [
        // // Girls' Generation
        { groupID: 3, gcount: 8, theme: 301 },
        { groupID: 3, gcount: 8, theme: 302 },
        { groupID: 3, gcount: 8, theme: 303 },
        { groupID: 3, gcount: 8, theme: 304 },
        // SHINee
        { groupID: 7, gcount: 5, theme: 701 },
        { groupID: 7, gcount: 5, theme: 702 },
        { groupID: 7, gcount: 5, theme: 703 },
        { groupID: 7, gcount: 5, theme: 704 },
        { groupID: 7, gcount: 5, theme: 705 },
        { groupID: 7, gcount: 5, theme: 706 },
        { groupID: 7, gcount: 5, theme: 707 },
        // BoA
        { groupID: 8, gcount: 3, theme: 714 },
        { groupID: 8, gcount: 3, theme: 801 },
        { groupID: 8, gcount: 3, theme: 802 },
        { groupID: 8, gcount: 3, theme: 803 },
        { groupID: 8, gcount: 3, theme: 804 },
        { groupID: 8, gcount: 3, theme: 805 },
        { groupID: 8, gcount: 3, theme: 806 },
        { groupID: 8, gcount: 3, theme: 807 },
        { groupID: 8, gcount: 3, theme: 809 },
        { groupID: 8, gcount: 3, theme: 811 },
        // TVXQ!
        { groupID: 9, gcount: 4, theme: 901 },
        { groupID: 9, gcount: 4, theme: 902 },
        { groupID: 9, gcount: 4, theme: 903 },
        { groupID: 9, gcount: 4, theme: 904 },
        { groupID: 9, gcount: 4, theme: 905 },
        { groupID: 9, gcount: 4, theme: 906 },
        { groupID: 9, gcount: 4, theme: 908 },
        // Red Velvet
        { groupID: 12, gcount: 5, theme: 1201 },
        { groupID: 12, gcount: 5, theme: 1202 },
        { groupID: 12, gcount: 5, theme: 1203 },
        { groupID: 12, gcount: 5, theme: 1204 },
        { groupID: 12, gcount: 5, theme: 1205 },
        { groupID: 12, gcount: 5, theme: 1206 },
        { groupID: 12, gcount: 5, theme: 1207 },
        { groupID: 12, gcount: 5, theme: 1208 },
        { groupID: 12, gcount: 5, theme: 1209 },
        { groupID: 12, gcount: 5, theme: 1211 },
        { groupID: 12, gcount: 5, theme: 1213 },
        { groupID: 12, gcount: 5, theme: 1214 },
        { groupID: 12, gcount: 5, theme: 1216 },
        { groupID: 12, gcount: 5, theme: 1220 },
        { groupID: 12, gcount: 5, theme: 1224 },
        // NCT DREAM (Déjà Vu)
        { groupID: 29, gcount: 7, theme: 2901 },
        { groupID: 29, gcount: 7, theme: 2902 },
        { groupID: 29, gcount: 7, theme: 2903 },
        { groupID: 29, gcount: 7, theme: 2905 },
        // EXO
        { groupID: 36, gcount: 8, theme: 3601 },
        { groupID: 36, gcount: 8, theme: 3602 },
        { groupID: 36, gcount: 8, theme: 3603 },
        { groupID: 36, gcount: 8, theme: 3604 },
        { groupID: 36, gcount: 8, theme: 3605 },
        { groupID: 36, gcount: 8, theme: 3606 },
        { groupID: 36, gcount: 8, theme: 3607 },
        { groupID: 36, gcount: 8, theme: 3610 },
        // NCT 127 (Neo Zone)
        { groupID: 50, gcount: 9, theme: 5002 },
        { groupID: 50, gcount: 9, theme: 5004 },
        { groupID: 50, gcount: 9, theme: 5006 },
        { groupID: 50, gcount: 9, theme: 5008 },
        // SuperM
        { groupID: 7000, gcount: 7, theme: 7000 },
        { groupID: 7000, gcount: 7, theme: 7002 },
        { groupID: 7000, gcount: 7, theme: 7004 },
        { groupID: 7000, gcount: 7, theme: 7005 },
        // SUPER JUNIOR (The Renaissance)
        { groupID: 8000, gcount: 9, theme: 8000 },
        { groupID: 8000, gcount: 9, theme: 8001 },
        { groupID: 8000, gcount: 9, theme: 8002 },
        { groupID: 8000, gcount: 9, theme: 8004 },
        // KANGTA
        { groupID: 32, gcount: 3, theme: 3201 },
        // WayV
        { groupID: 15000, gcount: 7, theme: 15000 },
        { groupID: 15000, gcount: 7, theme: 15001 },
    ],
};

async function main() {
    const appNames = Object.keys(CHUNKS);
    appNames.forEach(async (appName) => {
        const cs = CHUNKS[appName];
        for (const c of cs) {
            console.time(`${appName}, ${c.groupID}, ${c.theme}, ${c.gcount}`);
            const CNT = await fetchRow(appName, c.groupID, c.theme, c.gcount);
            console.timeEnd(`${appName}, ${c.groupID}, ${c.theme}, ${c.gcount}`);
            console.log(appName, c.groupID, c.theme, c.gcount, CNT);
        }
    });
}

main();

