const xlsx = require("xlsx");

const path = require('path');
const fs = require('fs');

var filename = "AudioSheets.xlsx";
var workbook = xlsx.readFile(filename);


workbook.SheetNames.forEach(sheetName => {
    if(sheetName.toLowerCase().includes("not_")) return;

    const myHeader = ["id","keyName","path"];

    let jsonArr = [];
    
    const range = xlsx.utils.decode_range(workbook.Sheets[sheetName]['!ref']);
    range.s['r'] = 1;
    range.e['c'] = myHeader.length - 1;
    workbook.Sheets[sheetName]['!ref'] = xlsx.utils.encode_range(range);
    for (var R = range.s.r; R <= range.e.r; ++R) {
        var row = {};
        for (var C = range.s.c; C <= range.e.c; ++C) {
            var cell_address = { c: C, r: R };
            var data = xlsx.utils.encode_cell(cell_address);

            const cellValue = workbook.Sheets[sheetName][data] ? workbook.Sheets[sheetName][data].v : '';
            if (cellValue === '') { continue; }

            if(C == 0) row['id'] = workbook.Sheets[sheetName][data].v;
            else if(C == 1) row['keyName'] = workbook.Sheets[sheetName][data].v;
            else if(C == 2) {
                var fileFullPath = workbook.Sheets[sheetName][data].v;
                var fileName = path.parse(fileFullPath).base;
                var fileDir = path.parse(fileFullPath).dir;
                
                row['path'] = "Sounds/" + fileDir.toUpperCase().concat('/').concat(fileName);
            }
        }
        if(Object.keys(row).length > 0){
            jsonArr.push(row);
        }
    }

    let outputPath = './output';

    if(!fs.existsSync(outputPath)){
        fs.mkdirSync(outputPath);
    }

    const sheetJson = JSON.stringify(jsonArr);
    const jsonFilePath = path.join(outputPath, sheetName + ".json");

    fs.writeFileSync(jsonFilePath, sheetJson);
});