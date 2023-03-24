process.env.TZ = 'Asia/Seoul';

const fs = require('fs');
const mysql = require('mysql2/promise');
const Async = require('async');
const moment = require('moment');

const CONFIG = require('./config.js');
const { SSS } = require('./config.js');

async function fetchRow(appName, worldRecordDataCode, createdAt, begin, end) {
    const CFG = CONFIG[appName];
    const con = await mysql.createConnection({
        host: CFG.DB.host,
        port: CFG.DB.port,
        user: CFG.DB.user,
        password: CFG.DB.password,
    });
    await con.connect();

    const owners = {};
    const typeOfItems = [];
    
    // await con.execute(`SET @B = '${begin}'`);
    // await con.execute(`SET @E = '${end}'`);
    await con.execute(`SET @createdAt = '${createdAt}'`);
    
    //모든 보상 메시지를 조회한다.
    const [result1] = await con.execute(`
        SELECT * 
        FROM ${CFG.DB.account_db}1.Message MSG 
        WHERE MSG.createdAt = @createdAt AND MSG.provideItem = 70000 AND MSG.owner = 1249422
        union all
        SELECT * 
        FROM ${CFG.DB.account_db}2.Message MSG 
        WHERE MSG.createdAt = @createdAt AND MSG.provideItem = 70000 AND MSG.owner = 1249422
    `);
    
    result1.forEach(row => {
        if(!owners[row.owner]) {
            owners[row.owner] = {
                items : [],
                rewards_song_count : 0,
                first_song_count : 0,
                subjects : [],
                owner : row.owner,
                songNames : []
            }
        }

        if(!typeOfItems.includes(row.provideItem)) typeOfItems.push(row.provideItem);

        //유저별 보상 종류 및 수량 누적
        //받은 보상의 subject 값 누적 ---> 보상 지급 곡 수 카운팅용, 1등 곡 수 카운팅용
        // var songName = row.subject.match(/".*?"/g)[0];
        var songName = row.subject.substring(row.subject.indexOf('>') + 1, row.subject.indexOf('월드') - 1);
        if(songName && !owners[row.owner]['songNames'].includes(songName)){
            owners[row.owner]['songNames'].push(songName);

            if(row.subject.includes('1위')) owners[row.owner]['first_song_count']++;

            owners[row.owner]['rewards_song_count']++;
        }
        
        var filter = owners[row.owner]['items'].filter(x => x.provideItem == row.provideItem);
        if(filter.length == 0){
            owners[row.owner]['items'].push({
                provideItem : row.provideItem, 
                quantity : parseInt(row.quantity)
            });
        }
        else{
            filter[0].quantity += parseInt(row.quantity);
        }
    });

    typeOfItems.sort(function(a, b) { // 오름차순
        return a - b;
    });

    await con.end();

    let results = [];

    let header = `owner\treward_count\trank1_count`;
    for (const provideItem of Object.values(typeOfItems)) {
        header += `\t${provideItem}`;
    }
    results.push(header);

    Object.values(owners).forEach(owner => {
        let str = '';
        for(let i=0; i<typeOfItems.length; i++){
            const type = typeOfItems[i];
            var filter = owner['items'].filter(x => x.provideItem == type);
            if(filter.length == 0){
                owner['items'].push({
                    provideItem : type, 
                    quantity : 0
                });
            }
        }

        owner['items'].sort(function(a, b){
            return a.provideItem - b.provideItem;
        });

        owner['items'].forEach(item => {
            str += `\t${item.quantity}`;
        });
        results.push(`${owner.owner}\t${owner.rewards_song_count}\t${owner.first_song_count}${str}`);
    });
    return results.join('\r\n');
}


const CHUNKS = {
    SSM: [
        {
            worldRecordDataCode: 7,
            begin: '2021-01-06 14:00:00',
            end: '2021-03-31 23:59:59',
            createdAt: '2021-04-07 14:13:15'
        }
    ],
    SSJ: [
        {
            worldRecordDataCode: 3,
            begin: '2020-01-01 00:00:00',
            end: '2020-03-31 23:59:59',
            createdAt: '2020-04-02 14:13:56'
        },
        {
            worldRecordDataCode: 4,
            begin: '2020-04-01 00:00:00',
            end: '2020-06-30 23:59:59',
            createdAt: '2020-07-02 14:32:53'
        },
        {
            worldRecordDataCode: 5,
            begin: '2020-07-09 14:00:00',
            end: '2020-07-31 23:59:59',
            createdAt: '2020-08-06 15:16:56'
        },
        {
            worldRecordDataCode: 6,
            begin: '2020-08-06 14:00:00',
            end: '2020-09-30 23:59:59',
            createdAt: '2020-10-08 14:26:05'
        },
        {
            worldRecordDataCode: 7,
            begin: '2020-10-08 14:00:00',
            end: '2020-12-31 23:59:59',
            createdAt: '2021-01-07 14:19:46'
        },
        {
            worldRecordDataCode: 8,
            begin: '2021-01-07 14:00:00',
            end: '2021-03-31 23:59:59',
            createdAt: '2021-04-01 14:18:11'
        }
    ],
    SSW: [
        {
            worldRecordDataCode: 1,
            begin: '2020-04-21 00:00:00',
            end: '2020-06-30 23:59:59',
            createdAt: '2020-07-09 14:12:53'
        },
        {
            worldRecordDataCode: 2,
            begin: '2020-07-09 14:00:00',
            end: '2020-07-31 23:59:59',
            createdAt: '2020-08-06 15:05:25'
        },
        {
            worldRecordDataCode: 3,
            begin: '2020-08-06 14:00:00',
            end: '2020-09-30 23:59:59',
            createdAt: '2020-10-08 14:16:07'
        },
        {
            worldRecordDataCode: 4,
            begin: '2020-10-08 14:00:00',
            end: '2020-12-31 23:59:59',
            createdAt: '2021-01-07 14:14:37'
        },
        {
            worldRecordDataCode: 5,
            begin: '2021-01-07 14:00:00',
            end: '2021-03-31 23:59:59',
            createdAt: '2021-04-01 14:24:49'
        }
    ],
    SSS: [
        {
            worldRecordDataCode: 3,
            begin: '2020-04-01 00:00:00',
            end: '2020-06-29 23:59:59',
            createdAt: '2020-07-01 14:17:08'
        },
        {
            worldRecordDataCode: 4,
            begin: '2020-07-01 00:00:00',
            end: '2020-07-31 23:59:59',
            createdAt: '2020-08-05 16:23:28'
        },
        {
            worldRecordDataCode: 5,
            begin: '2020-08-05 14:00:00',
            end: '2020-09-30 23:59:59',
            createdAt: '2020-10-06 14:17:38'
        },
        {
            worldRecordDataCode: 6,
            begin: '2020-10-06 14:00:00',
            end: '2020-12-31 23:59:59',
            createdAt: '2021-01-06 14:48:00'
        },
        {
            worldRecordDataCode: 7,
            begin: '2021-01-06 14:00:00',
            end: '2021-03-31 23:59:59',
            createdAt: '2021-04-06 14:10:53'
        }
    ],
    SSG: [
        {
            worldRecordDataCode: 3,
            begin: '2020-04-01 00:00:00',
            end: '2020-06-30 23:59:59',
            createdAt: '2020-07-07 14:19:19'
        },
        {
            worldRecordDataCode: 4,
            begin: '2020-07-01 00:00:00',
            end: '2020-09-30 23:59:59',
            createdAt: '2020-10-06 14:15:31'
        },
        {
            worldRecordDataCode: 5,
            begin: '2020-10-06 14:00:00',
            end: '2020-12-31 23:59:59',
            createdAt: '2021-01-06 14:30:55'
        },
        {
            worldRecordDataCode: 6,
            begin: '2021-01-06 14:00:00',
            end: '2021-03-31 23:59:59',
            createdAt: '2021-04-06 14:09:05'
        }
    ]
};

// async function main() {
//     const appNames = Object.keys(CHUNKS);
//     appNames.forEach(async (appName) => {
//         const cs = CHUNKS[appName];
//         for (const c of cs) {
//             const contents = await fetchRow(appName, c.worldRecordDataCode, c.createdAt, c.begin, c.end);
//             fs.writeFileSync(`./${appName}_${c.worldRecordDataCode}.csv`, contents, 'utf8');
//         }
//     });
// }

async function main(appName) {
const cs = CHUNKS[appName];
    for (const c of cs) {
        const contents = await fetchRow(appName, c.worldRecordDataCode, c.createdAt, c.begin, c.end);
        fs.writeFileSync(`./${appName}_${c.worldRecordDataCode}_test.csv`, contents, 'utf8');
    }
}

main('SSM');
