const REPORT_DEFAULT_COLUMNS = [
    /* A  */ { wch: 0.64 }, /* B  */ { wch: 0.64 }, /* C  */ { wch: 3.83 }, /* D  */ { wch: 3.83 },
    /* E  */ { wch: 3.83 }, /* F  */ { wch: 3.83 }, /* G  */ { wch: 3.83 }, /* H  */ { wch: 3.83 },
    /* I  */ { wch: 3.83 }, /* J  */ { wch: 3.83 }, /* K  */ { wch: 3.83 }, /* L  */ { wch: 3.83 },
    /* M  */ { wch: 3.83 }, /* N  */ { wch: 3.83 }, /* O  */ { wch: 3.83 }, /* P  */ { wch: 3.83 },
    /* Q  */ { wch: 3.83 }, /* R  */ { wch: 3.83 }, /* S  */ { wch: 3.83 }, /* T  */ { wch: 3.83 },
    /* U  */ { wch: 3.83 }, /* V  */ { wch: 3.83 }, /* W  */ { wch: 3.83 }, /* X  */ { wch: 3.83 },
    /* Y  */ { wch: 3.83 }, /* Z  */ { wch: 3.83 }, /* AA */ { wch: 3.83 }, /* AB */ { wch: 3.83 },
    /* AC */ { wch: 3.83 }, /* AD */ { wch: 3.83 }, /* AE */ { wch: 3.83 }, /* AF */ { wch: 0.64 },
    /* AF */ { wch: 0.64 },
];
const REPORT_DEFAULT_ROWS = [
    /* 1 */ { hpt: 6  }, /* 2 */ { hpt: 15 }, /* 3 */ { hpt: 15 }, /* 4 */ { hpt: 15 },
    /* 5 */ { hpt: 15 }, /* 6 */ { hpt: 15 }, /* 7 */ { hpt: 15 },
];
const REPORT_DEFAULT_MERGES = [
    { s: { c: 11, r: 3 }, e: { c: 21, r: 4 } }, // 프로젝트 제목
    { s: { c: 25, r: 3 }, e: { c: 26, r: 3 } }, // 보고일자
    { s: { c: 27, r: 3 }, e: { c: 29, r: 3 } }, // 보고일자 내용
    { s: { c: 25, r: 4 }, e: { c: 26, r: 4 } }, // 작성자
    { s: { c: 27, r: 4 }, e: { c: 29, r: 4 } }, // 작성자 내용
];

//현재 화면 내려받기에 대한 Column 설정
const SINGLE_REPORT_COLUMNS = [
    /* A  */ { wch: 0.77 }, /* B  */ { wch: 0.77 }, /* C  */ { wch: 4 }, /* D  */ { wch: 4 },
    /* E  */ { wch: 4 }, /* F  */ { wch: 4 }, /* G  */ { wch: 4 }, /* H  */ { wch: 4 },
    /* I  */ { wch: 4 }, /* J  */ { wch: 4 }, /* K  */ { wch: 4 }, /* L  */ { wch: 4 },
    /* M  */ { wch: 4 }, /* N  */ { wch: 4 }, /* O  */ { wch: 4 }, /* P  */ { wch: 4 },
    /* Q  */ { wch: 4 }, /* R  */ { wch: 4 }, /* S  */ { wch: 4 }, /* T  */ { wch: 4 },
    /* U  */ { wch: 4 }, /* V  */ { wch: 4 }, /* W  */ { wch: 4 }, /* X  */ { wch: 4 },
    /* Y  */ { wch: 4 }, /* Z  */ { wch: 4 }, /* AA  */ { wch: 4 }, /* AB  */ { wch: 4 },
    /* AC  */ { wch: 4 }, /* AD  */ { wch: 4 }, /* AE  */ { wch: 4 }, /* AF */ { wch: 0.77 },
    /* AG */ { wch: 0.77 }
]

function initReportTemplate(sheet) {
    if (!sheet) {
        sheet = XLSX.utils.aoa_to_sheet([]);
    }

    sheet['!cols'] = [...REPORT_DEFAULT_COLUMNS];
    sheet['!rows'] = [...REPORT_DEFAULT_ROWS];
    sheet['!ref'] = XLSX.utils.encode_range({
        s: { c: 0, r: 0 }, e: { c: 32, r: 6 }
    });
    sheet['!merges'] = [...REPORT_DEFAULT_MERGES];

    XLSX.utils.sheet_set_range_style(sheet, 'A1:AG200', {
        fgColor: { rgb: 0xFFFFFF }
    });

    sheet['L4']  = {
        t: 's', v: 'SUPERSTAR DAILY 지표현황',
        s: {
            sz: '16',
            bold: true,
            alignment: {
                vertical: 'center',
                horizontal: 'center'
            },
        }
    };
    XLSX.utils.sheet_set_range_style(sheet, 'C3:AE6', {
        top: { style: 'medium' }, bottom: { style: 'medium' },
        left: { style: 'medium' }, right: { style: 'medium' }
    });

    sheet['Z4']  = {
        t: 's', v: '보고일자',
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };
    XLSX.utils.sheet_set_range_style(sheet, 'Z4:AA4', {
        top: { style: 'thin' }, bottom: { style: 'thin' },
        left: { style: 'thin' }, right: { style: 'thin' }
    });

    sheet['AB4'] = {
        t: 's', v: moment().format('YYYY/M/D'),
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };
    XLSX.utils.sheet_set_range_style(sheet, 'AB4:AD4', {
        top: { style: 'thin' }, bottom: { style: 'thin' },
        left: { style: 'thin' }, right: { style: 'thin' }
    });

    sheet['Z5']  = {
        t: 's', v: '작성자',
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };
    XLSX.utils.sheet_set_range_style(sheet, 'Z5:AA5', {
        top: { style: 'thin' }, bottom: { style: 'thin' },
        left: { style: 'thin' }, right: { style: 'thin' }
    });

    sheet['AB5'] = {
        t: 's', v: $('#username').val(),
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };
    XLSX.utils.sheet_set_range_style(sheet, 'AB5:AD5', {
        top: { style: 'thin' }, bottom: { style: 'thin' },
        left: { style: 'thin' }, right: { style: 'thin' }
    });

    sheet['!images'] = [{
        '!link': 'https://tools.dalcomsoft.net/static/image/logo.png',
        '!pos': { x: 45, y: 53, w: 210, h: 30 },
        '!datatype': 'remote'
    }];

    return sheet;
}

//현재 화면 내려받기에 대한 템플릿
function initSingleReportTemplate(args){
    const type = args.type;
    var sheet = args.sheet != undefined ? args.sheet : XLSX.utils.aoa_to_sheet([]);
    const data = args.data;
    const headerText = args.headerText;
    const appName = args.appName;

    switch(type){
        case 'kpi-report-tab':
            sheet = appendSingleReportTemplate({
                bg_range : 'A1:AG100',
                outline_range : 'C3:AE6',
                sheet : sheet,
                headerText : headerText
            });
            sheet = appendReporterLabel_v1(sheet);
            if(!sheet['!images']) sheet['!images'] = [];

            if(!appName){
                const img_address = $('#report-title img').attr('src');
                if(img_address){
                    sheet['!images'].push({
                        '!link': img_address,
                        '!pos': { x: 400, y: 48, w: 40, h: 40 },
                        '!datatype': 'remote'
                    });
                }
            }
            else{
                const icon = (projects.find(project => project.shortName === appName) || {}).icon;
                if(icon !== undefined){
                    sheet['!images'].push({
                        '!link': icon,
                        '!pos': { x: 400, y: 48, w: 40, h: 40 },
                        '!datatype': 'remote'
                    });
                }
            }
            break;
        case 'kpi-analytics-tab':
            var cell_ref_begin = XLSX.utils.encode_cell({ c : 0, r : 0 });
            var cell_ref_end = XLSX.utils.encode_cell({ c : data[0].length + 2, r : 5 + data.length });

            sheet['!cols'] = [];
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});
            for(let i = 0; i < data[0].length + 2; i++){
                sheet['!cols'].push({
                    wch: 10
                });
            }
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});

            XLSX.utils.sheet_set_range_style(sheet, cell_ref_begin + ':' + cell_ref_end, {
                fgColor: { rgb: 0xFFFFFF }
            });

            sheet['!ref'] = XLSX.utils.encode_range({
                s: { c: 0, r: 0 }, e: { c: data[0].length + 2, r : 10 + data.length }
            });
            break;
        case 'kpi-ad-tab':
            var cell_ref_begin = XLSX.utils.encode_cell({ c : 0, r : 0 });
            var cell_ref_end = XLSX.utils.encode_cell({ c : data[0].length + 2, r : 5 + data.length });

            sheet['!cols'] = [];
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});
            for(let i = 0; i < data[0].length + 2; i++){
                sheet['!cols'].push({
                    wch: 8.5
                });
            }
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});

            XLSX.utils.sheet_set_range_style(sheet, cell_ref_begin + ':' + cell_ref_end, {
                fgColor: { rgb: 0xFFFFFF }
            });

            sheet['!ref'] = XLSX.utils.encode_range({
                s: { c: 0, r: 0 }, e: { c: data[0].length + 2, r : 10 + data.length }
            });
            break;
        case 'kpi-daily-report-tab':
            //일일지표 현황
            sheet = appendSingleReportTemplate({
                bg_range : 'A1:AG100',
                outline_range : 'C3:AE6',
                sheet : sheet,
                headerText : ''
            });

            sheet['M4']  = {
                t: 's', v: headerText,
                s: {
                    sz: '16',
                    bold: true,
                    alignment: {
                        vertical: 'center',
                        horizontal: 'center'
                    },
                }
            };

            sheet = appendReporterLabel_v1(sheet);
            break;
        case 'kpi-daily-tab':
            var cell_ref_begin = XLSX.utils.encode_cell({ c : 0, r : 0 });
            var cell_ref_end = XLSX.utils.encode_cell({ c : data[0].length + 2, r : 5 + data.length });

            sheet['!cols'] = [];
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});
            for(let i = 0; i < data[0].length + 2; i++){
                sheet['!cols'].push({
                    wch: 10
                });
            }
            sheet['!cols'].push({wch : 0.77});
            sheet['!cols'].push({wch : 0.77});

            XLSX.utils.sheet_set_range_style(sheet, cell_ref_begin + ':' + cell_ref_end, {
                fgColor: { rgb: 0xFFFFFF }
            });

            sheet['!ref'] = XLSX.utils.encode_range({
                s: { c: 0, r: 0 }, e: { c: data[0].length + 2, r : 10 + data.length }
            });
            break;
    }
    return sheet;
}

//현재 화면 내려받기에 대한 템플릿
function appendSingleReportTemplate(args){
    const bg_range = args.bg_range;
    const sheet = args.sheet;
    const headerText = args.headerText;
    const outline_range = args.outline_range;

    sheet['!cols'] = [...SINGLE_REPORT_COLUMNS];
    sheet['!rows'] = [...REPORT_DEFAULT_ROWS];

    //33(AG에 해당)
    sheet['!ref'] = XLSX.utils.encode_range({
        s: { c: 0, r: 0 }, e: { c: 33, r: 100 }
    });

    XLSX.utils.sheet_set_range_style(sheet, bg_range, {
        fgColor: { rgb: 0xFFFFFF }
    });

    XLSX.utils.sheet_set_range_style(sheet, 'B2:AF99', {
        top: { style: 'medium', color: { rgb: '#ededeb' } },
        bottom: { style: 'medium', color : { rgb: '#ededeb' }},
        left: { style: 'medium', color : { rgb: '#ededeb' }},
        right: { style: 'medium', color : { rgb: '#ededeb' }}
    });

    if(!sheet['!merges']) sheet['!merges'] = [];

    sheet['!merges'].push(
        {
            s: { c: 12, r: 3},
            e: { c: 18, r: 4}
        }
    );

    sheet['M4']  = {
        t: 's', v: headerText,
        s: {
            sz: '16',
            bold: true,
            alignment: {
                vertical: 'center',
                horizontal: 'center'
            },
        }
    };
    XLSX.utils.sheet_set_range_style(sheet, outline_range, {
        top: { style: 'medium' }, bottom: { style: 'medium' },
        left: { style: 'medium' }, right: { style: 'medium' }
    });
    return sheet;
}

//현재화면 내려받기용 상단 헤더 포멧
function appendReporterLabel_v1(sheet){
    if(!sheet['!merges']) sheet['!merges'] = [];

    var merges = [];

    merges.push(
        {
            s: { c: 25, r: 3 },
            e: { c: 26, r: 3 }
        },
        {
            s: { c: 25, r: 4 },
            e: { c: 26, r: 4 }
        },
        {
            s: { c: 27, r: 3 },
            e: { c: 29, r: 3 }
        },
        {
            s: { c: 27, r: 4 },
            e: { c: 29, r: 4 }
        }
    );

    sheet['!merges'].push(...merges);

    sheet['Z4']  = {
        t: 's', v: '보고일자',
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };

    sheet['Z5']  = {
        t: 's', v: '작성자',
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };

    sheet['AB4'] = {
        t: 's', v: moment().format('YYYY/M/D'),
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };

    sheet['AB5'] = {
        t: 's', v: $('#username').val(),
        s: { sz: '10', alignment: { horizontal: 'center' }}
    };

    XLSX.utils.sheet_set_range_style(sheet, 'Z4:AD5', {
        top: { style: 'thin' }, bottom: { style: 'thin' },
        left: { style: 'thin' }, right: { style: 'thin' }
    });

    if(!sheet['!images']) sheet['!images'] = [];

    sheet['!images'].push({
        '!link': 'https://tools.dalcomsoft.net/static/image/logo.png',
        '!pos': { x: 60, y: 50, w: 210, h: 30 },
        '!datatype': 'remote'
    });
    return sheet;
}

function appendReportTableToSheet(sheet, title, headerNames, header, data) {
    const range = XLSX.utils.decode_range(sheet['!ref']);
    const begin = range.e.r + 1;

    range.e.r += data.length + 3;
    sheet['!ref'] = XLSX.utils.encode_range(range);

    const titleCell = XLSX.utils.encode_cell({ c: 2, r: begin });
    sheet[titleCell] = { t: 's', v: title, s: { sz: '10' }};
    sheet['!rows'][begin] = { hpt: 15 };

    for (let col = 0; col < 10; col++) {
        const c = col ? ((col - 1) * 3 + 4) : 2;
        const c2 = c + (col ? 2 : 1);

        // 일간 프로젝트별 매출 해더
        const cell_ref = XLSX.utils.encode_cell({ c, r: begin + 1 });
        const cell_range = XLSX.utils.encode_range({
            s: { c, r: begin + 1 },
            e: { c: c2, r: begin + 1 }
        });
        sheet[cell_ref] = {
            t: 's',
            v: headerNames[col] !== undefined ? headerNames[col] : '',
            s: { sz: '10' }
        };
        XLSX.utils.sheet_set_range_style(sheet, cell_range, {
            fgColor: { rgb: 0xEFEFEF },
            top: { style: 'medium', color: { rgb: 0xDEDEDE } },
            bottom: { style: 'medium', color: { rgb: 0xDEDEDE } },
            left: { style: 'medium', color: { rgb: 0xDEDEDE } },
            right: { style: 'medium', color: { rgb: 0xDEDEDE } },
            alignment: { horizontal: 'center' }
        });
        sheet['!merges'].push({
            s: { c, r: begin + 1 },
            e: { c: c2, r: begin + 1 }
        });
        if (col === 0) {
            sheet['!rows'][begin + 1] = { hpt: 15 };
        }

        // 일간 프로젝트별 매출 내용
        data.forEach((value, row) => {
            const r = begin + row + 2;
            const cell_range = {
                s: { c, r },
                e: { c: c2, r }
            };
            const cell_ref = XLSX.utils.encode_cell(cell_range.s);
            const cell_range_ref = XLSX.utils.encode_range(cell_range);
            sheet[cell_ref] = { t: 's', v: value[header[col]], s: { sz: '10' }};
            if (col === 0) {
                XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                    fgColor: { rgb: 0xEFEFEF },
                    top: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    bottom: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    left: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    right: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    alignment: { horizontal: 'center' }
                });
            } else {
                if (value[header[col]] === 0 || !isNaN(value[header[col]])) {
                    sheet[cell_ref].t = 'n';
                    sheet[cell_ref].z = '#,##0';
                }
                if (col === 1) {
                    XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                        fgColor: { rgb: 0xFFF2CC },
                        top: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        bottom: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        left: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        right: { style: 'thin', color: { rgb: 0xDEDEDE } }
                    });
                } else {
                    XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                        top: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        bottom: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        left: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        right: { style: 'thin', color: { rgb: 0xDEDEDE } }
                    });
                }
            }
            sheet['!merges'].push(cell_range);
            if (col === 0) {
                sheet['!rows'][r] = { hpt: 15 };
            }
        });
    }
    sheet['!rows'].push({ hpt: 15 });
}

//서버 데이터 선택 -> 지표현황 추출
function genReportSheet(res, app, endDate) {
    let sheet = initReportTemplate();

    let rows = [];
    (Object.values(res.reportMonthly)).forEach(row => {
        rows.push(Object.values(row));
    });

    let headers = [[]];
    for(let i=6; i>=0; i--){
        headers[0].push(moment(endDate).subtract(i, 'day').locale('kr').format('YY/MM/DD(ddd)'));
    }

    const data = headers.concat(rows);

    const range = XLSX.utils.decode_range(sheet['!ref']);
    range.e.r += rows.length + 4;
    sheet['!ref'] = XLSX.utils.encode_range(range);

    const canvas = $(`#kpi-export-chart #export_report_${app}_canvas`)[0];
    if((app !== 'SS') && (app !== 'TOTAL')) {
        const osCanvas = $(`#kpi-export-os-chart #export_report_${app}_os_canvas`)[0];
        sheet = exportReports('kpi-report-tab', data, { canvas, osCanvas }, app);
    }
    else {
        sheet = exportReports('kpi-report-tab', data, { canvas }, app);
    }

    sheet = _genWeeklyReportSheet(sheet, res.reportWeekly, headers[0]);
    sheet = _genMonthlyShortSheet(
        sheet, 
        res.reportMonthly, 
        app
    );

    return sheet;
}

//서버 데이터 선택 -> 지표현황 -> 주간 지표 현황 시트 생성
function _genWeeklyReportSheet(sheet, _data, _headers){
    const searchDate = $('#searchDate').val();
    var data = Object.values(_data).sort((a, b) => {
        // AD가 앞에..
        const [aName, aLocal] = a.item.replace(' 이슈', '').split('_');
        const [bName, bLocal] = b.item.replace(' 이슈', '').split('_');

        const aOrder = itemOrder[a.item] || ((aLocal ? 100 : 0) - projectOrder.indexOf(aName));
        const bOrder = itemOrder[b.item] || ((bLocal ? 100 : 0) - projectOrder.indexOf(bName));
        
        return aOrder - bOrder;
    });

    let headers = [[
        '항목', '금주 Total', '지난주 Total',
    ]];
    headers[0].push(..._headers);
    let filtered_data = [[]];
    filtered_data[0] = headers[0];
    for(let row=0; row<data.length; row++){
        let isIssue = data[row].item.includes('이슈');
        if(isIssue){
            filtered_data[row + 1] = [
                data[row].item,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ];
        }
        else{
            const row_arr = Object.values(data[row]);
            let tmp_arr = [
                data[row].item, 
                data[row]['curWeek'], 
                data[row]['prevWeek'], 
                ...row_arr.slice(0, row_arr.length - 3),
                null,null,null,null,null,null
            ];
            filtered_data[row + 1] = tmp_arr;
        }
    }
    let sheet_write_index = { r : 26, c : 2 };
    sheet = genWeeklyReportSheet(filtered_data, sheet, sheet_write_index);
    return sheet;
}

//서버 데이터 선택 -> 지표현황 -> 월간 지표 시트 생성
function _genMonthlyShortSheet(sheet, _data, app){
    const searchDate = $('#searchDate').val();

    let data = Object.values(_data).sort((a, b) => {
        const aOrder = monthlyItemOrder[a.item];
        const bOrder = monthlyItemOrder[b.item];

        return aOrder - bOrder;
    });

    let headers = [[]];
    headers[0] = ['항목'];

    for (let i = 0; i < 3; i++) {
        if (i === 1) {
            headers[0].push('전월 대비');
        }
        headers[0].push(moment(searchDate).subtract(i, 'month').locale('kr').format('M월'));
    }

    let filtered_data = [[]];
    filtered_data[0] = headers[0];
    for(let row=0; row<data.length; row++){
        const row_arr = Object.values(data[row]);
        let tmp_arr = [
            data[row].item,
            row_arr[0],
            row_arr[0] / row_arr[1] - 1,
            ...row_arr.slice(1, row_arr.length - 2)
        ];
        filtered_data[row + 1] = tmp_arr;
    }

    if(app === 'SS') sheet = genMonthly_small_ReportSheet(filtered_data, sheet, { c : 2, r : 60 });
    else if(app === 'TOTAL') sheet = genMonthly_small_ReportSheet(filtered_data, sheet, { c : 2, r : 63 });
    else sheet = genMonthly_small_ReportSheet(filtered_data, sheet, { c : 16, r : 53 });
    
    return sheet;
}

//서버 데이터 선택 -> KPI 추출
function genAnalyticsSheet(res) {
    let sheet = initReportTemplate();
    
    let rows = [];
    Object.values(res).forEach(row => {
        let newArr = {};

        newArr['일자/항목'] = row['date'];
        newArr['업데이트'] = '';
        newArr['이슈'] = '';

        newArr['설치합계'] = row['installs_Android'] + row['installs_iOS'];
        newArr['AOS-설치'] = row['installs_Android'];
        newArr['IOS-설치'] = row['installs_iOS'];
        
        newArr['NRU'] = row['NRU'];
        newArr['DAU'] = row['DAU'];
        newArr['PU'] = row['PU'];
        newArr['PUR'] = newArr['PU'] / newArr['DAU'];

        newArr['ARPPU'] = (row['revenue_Android'] + row['revenue_iOS'] + row['AD']) / newArr['PU'];
        newArr['ARPU'] = (row['revenue_Android'] + row['revenue_iOS'] + row['AD']) / newArr['DAU'];

        newArr['종합매출'] = row['revenue_Android'] + row['revenue_iOS'] + row['AD'];
        newArr['인앱매출'] = row['revenue_Android'] + row['revenue_iOS'];
        newArr['광고매출'] = row['AD'];

        newArr['광고비율'] = newArr['광고매출'] / newArr['종합매출'];

        newArr['AOS매출'] = row['revenue_Android'];
        newArr['IOS매출'] = row['revenue_iOS'];

        rows.push(Object.values(newArr));
    });

    const data = analyticsOptions.nestedHeaders.concat(rows);

    const range = XLSX.utils.decode_range(sheet['!ref']);
    range.e.r += rows.length + 4;
    sheet['!ref'] = XLSX.utils.encode_range(range);
    sheet = exportReports('kpi-analytics-tab', data);

    return sheet;
}

//서버 데이터 선택 -> 광고매출 추출
function genADSheet(data) {
    let sheet = initReportTemplate();
    let rows = [[]];

    const columns = ['date', 'Tapjoy', 'Tapjoy_KRW', 'ADColony', 'ADColony_KRW', 'AdMob', 'AdMob_KRW', 'AdGeneration', 'AdGeneration_KRW', 'ADCount', 'USD', 'JPY'];
    
    let index = 0;
    Object.values(data).forEach(row => {
        var newRow = [];
        columns.forEach(col => {
            var selected_val = row[col];
            if(selected_val) newRow.push(selected_val);
            else newRow.push('');
        });

        var adCount = 0;
        Object.keys(row).forEach(key => {
            if(key.includes('_KRW')){
                adCount += row[key];
            }
        });
        newRow[columns.indexOf('ADCount')] = adCount;

        rows[index] = newRow;
        index++;
    });

    const kpi_ad_nestedHeaders = [['Date', '[USD]Tapjoy', '[KRW]Tapjoy', '[USD]ADColony', '[KRW]ADColony', '[USD]Admob', '[KRW]Admob', '[USD]AdGeneration', '[KRW]AdGeneration', 'AD COUNT', '환율[1$]', '환율[100¥]']];
    const _data = kpi_ad_nestedHeaders.concat(rows);

    const range = XLSX.utils.decode_range(sheet['!ref']);
    range.e.r += rows.length + 4;
    sheet['!ref'] = XLSX.utils.encode_range(range);
    sheet = exportReports('kpi-ad-tab', _data);

    return sheet;
}

//서버 데이터 선택 -> 일일 지표현황
function genDailyReportSheet(res) {
    let sheet = initReportTemplate();
    let rows = [[]];
    rows[0] = res.nestedHeaders[0];
    res.data.forEach(row => {
        let tmp_arr = [];
        rows[0].forEach(key => {
            if(key === '항목') key = 'app';
            tmp_arr.push(row[key]);
        });
        rows.push(tmp_arr);
    });
    
    sheet = exportReports('kpi-daily-report-tab', rows);

    // appendReportTableToSheet(sheet, '■ 일간 프로젝트별 매출', headerNames, header, res.data);
    const range = XLSX.utils.decode_range(sheet['!ref']);
    range.e.r += res.data.length + 4;
    sheet['!ref'] = XLSX.utils.encode_range(range);

    return sheet;
}

//서버 데이터 선택 -> 일일 매출
function genDailySheet(res) {
    let sheet = initReportTemplate();
    let rows = [[]];
    rows[0] = res.colHeaders;
    res.data.forEach(row => {
        let tmp_arr = [];
        rows[0].forEach(key => {
            if(key === 'Date') key = 'date';
            if(key.includes(' 월별 누적')){
                key = key.replace(' 월별 누적', '_ACC');
            }
            let filtered_val = row[key] !== undefined ? row[key] : 0;
            tmp_arr.push(filtered_val);
        });
        rows.push(tmp_arr);
    });
    sheet = exportReports('kpi-daily-tab', rows);

    return sheet;
}

$(document).on('click', '#export_report_all', () => {
    const isChecked = $('#export_report_all').prop('checked');
    $('.export_report_items').each((_, e) => {
        $(e).prop('checked', isChecked);
    });
});

$(document).on('click', '#export_analytics_all', () => {
    const isChecked = $('#export_analytics_all').prop('checked');
    $('.export_analytics_items').each((_, e) => {
        $(e).prop('checked', isChecked);
    });
});

$(document).on('click', '#export_ad_all', () => {
    const isChecked = $('#export_ad_all').prop('checked');
    $('.export_ad_items').each((_, e) => {
        $(e).prop('checked', isChecked);
    });
});

$(document).on('click', '#export', () => {
    const begin = $('#exportBeginDate').val();
    const end = $('#exportEndDate').val();
    const promises = [];

    $('#exportBeginDate').prop('disabled', true);
    $('#exportEndDate').prop('disabled', true);

    $('#kpiExportDlg #export')
        .prop('disabled', true)
        .addClass('disabled')
        .append($('<span>', {
            class: 'spinner-border spinner-border-sm ml-2',
            role: 'status',
            'aria-hidden': 'true'
        }));

    $('#export_report_all').prop('disabled', true);

    $('.export_report_items').each((_, e) => {
        const elem = $(e);
        const id = elem.attr('id');
        elem.prop('disabled', true);
        if (elem.prop('checked')) {
            const app = id.replace('export_report_', '');
            $(`[for=${id}]`).append($('<span>', {
                class: 'spinner-border spinner-border-sm ml-2',
                role: 'status',
                'aria-hidden': 'true'
            }));

            promises.push(new Promise(resolve => {
                reportsLoad(app, end, (error, res) => {
                    $(`[for=${id}] .spinner-border`).remove();
                    $(`[for=${id}]`).append($('<i>', {
                        class: `fas ${error ? 'fa-times-circle text-danger'
                            : 'fa-check-circle text-success'} ml-2`
                    }));

                    $('#kpi-export-chart').append($('<canvas>', {
                        id : `${id}_canvas`,
                    }));

                    if(app !== 'SS' && app !== 'TOTAL'){
                        $('#kpi-export-os-chart').append($('<canvas>', {
                            id : `${id}_os_canvas`,
                        }));
                    }

                    if(!error){
                        var canvas = $(`#kpi-export-chart #${id}_canvas`)[0];
                        const data = res.reportMonth;
                        const rows = Object.values(data);
    
                        const chartOptions = reportChartOptions;
                        chartOptions.data.labels = Object.keys(data).map(key => moment(key).format('YY/M/D'));
                        chartOptions.data.datasets[0].data = rows.map(row => row.revenue);
                        chartOptions.data.datasets[1].data = rows.map(row => row.DAU);

                        $(canvas).css('visibility', 'visible');
                        $(canvas).css('width', '960');
                        $(canvas).css('height', '250');
                        new Chart(canvas, chartOptions);

                        if(app !== 'SS' && app !== 'TOTAL'){
                            var osCanvas = $(`#kpi-export-os-chart #${id}_os_canvas`)[0];
                            const os_data = res.revenueOSWeekly;
                            const osRows = Object.values(os_data);
    
                            const osChartOptions = weekRevenueChartOptions;
                            const searchDate = $('#searchDate').val();
    
                            osChartOptions.data.labels = Object.keys(os_data).reverse().map(i => moment(searchDate).subtract(i, 'day').locale('kr').format('MM.DD(ddd)'));
                            osChartOptions.data.datasets[0].data = osRows.map(row => row.android);
                            osChartOptions.data.datasets[1].data = osRows.map(row => row.ios);
                            $(osCanvas).css('visibility', 'visible');
                            $(osCanvas).css('width', '465');
                            $(osCanvas).css('height', '232');
                            new Chart(osCanvas, osChartOptions);
                        }

                        setTimeout(() => {
                            resolve({
                                sheetname: `${app}_Report`,
                                sheet: !error ? genReportSheet(res, app, end) : undefined,
                                error
                            });
                        }, 1200);
                        
                    }
                    else{
                        resolve({
                            sheetname: `${app}_Report`,
                            sheet: undefined,
                            error
                        });
                    }
                });
            }));
        }
    });

    $('#export_analytics_all').prop('disabled', true);
    $('.export_analytics_items').each((_, e) => {
        const elem = $(e);
        const id = elem.attr('id');
        elem.prop('disabled', true);
        if (elem.prop('checked')) {
            const app = id.replace('export_analytics_', '');
            $(`[for=${id}]`).append($('<span>', {
                class: 'spinner-border spinner-border-sm ml-2',
                role: 'status',
                'aria-hidden': 'true'
            }));

            promises.push(new Promise(resolve => {
                analyticsLoad(app, begin, end, (error, res) => {
                    $(`[for=${id}] .spinner-border`).remove();
                    $(`[for=${id}]`).append($('<i>', {
                        class: `fas ${error ? 'fa-times-circle text-danger'
                            : 'fa-check-circle text-success'} ml-2`
                    }));
                    resolve({
                        sheetname: `${app}_KPI`,
                        sheet: !error ? genAnalyticsSheet(res) : undefined,
                        error
                    });
                });
            }));
        }
    });

    $('#export_ad_all').prop('disabled', true);
    $('.export_ad_items').each((_, e) => {
        const elem = $(e);
        const id = elem.attr('id');
        elem.prop('disabled', true);
        if (elem.prop('checked')) {
            const app = id.replace('export_ad_', '');
            $(`[for=${id}]`).append($('<span>', {
                class: 'spinner-border spinner-border-sm ml-2',
                role: 'status',
                'aria-hidden': 'true'
            }));

            promises.push(new Promise(resolve => {
                adLoad(app, begin, end, (error, data) => {
                    $(`[for=${id}] .spinner-border`).remove();
                    $(`[for=${id}]`).append($('<i>', {
                        class: `fas ${error ? 'fa-times-circle text-danger'
                            : 'fa-check-circle text-success'} ml-2`
                    }));
                    resolve({
                        sheetname: `${app}_AD`,
                        sheet: !error ? genADSheet(data) : undefined,
                        error
                    });
                });
            }));
        }
    });

    $('#export_daily_report').prop('disabled', true);
    if ($('#export_daily_report').prop('checked')) {
        $(`[for=export_daily_report]`).append($('<span>', {
            class: 'spinner-border spinner-border-sm ml-2',
            role: 'status',
            'aria-hidden': 'true'
        }));

        promises.push(new Promise(resolve => {
            dailyReportLoad(end, (error, res) => {
                $(`[for=export_daily_report] .spinner-border`).remove();
                $(`[for=export_daily_report]`).append($('<i>', {
                    class: `fas ${error ? 'fa-times-circle text-danger'
                        : 'fa-check-circle text-success'} ml-2`
                }));
                resolve({
                    sheetname: `SS_Daily_Report`,
                    sheet: !error ? genDailyReportSheet(res) : undefined,
                    error
                });
            });
        }));
    }

    $('#export_daily').prop('disabled', true);
    if ($('#export_daily').prop('checked')) {
        $(`[for=export_daily]`).append($('<span>', {
            class: 'spinner-border spinner-border-sm ml-2',
            role: 'status',
            'aria-hidden': 'true'
        }));

        promises.push(new Promise(resolve => {
            dailyLoad(begin, end, (error, res) => {
                $(`[for=export_daily] .spinner-border`).remove();
                $(`[for=export_daily]`).append($('<i>', {
                    class: `fas ${error ? 'fa-times-circle text-danger'
                        : 'fa-check-circle text-success'} ml-2`
                }));
                resolve({
                    sheetname: `일일매출`,
                    sheet: !error ? genDailySheet(res) : undefined,
                    error
                });
            });
        }));
    }

    Promise.all(promises).then(values => {
        const wb = XLSX.utils.book_new();
        values.forEach(value => {
            if (value.error) {

            } else {
                XLSX.utils.book_append_sheet(wb, value.sheet, value.sheetname);
            }
        });
        if(wb !== undefined && Object.keys(wb.Sheets).length > 0){
            XLSX.writeFile(wb, `DALCOMSOFT_KPI_${begin}_${end}.xlsx`, {
                bookType: 'xlsx',
                bookSST: false,
                type: 'binary',
                showGridLines: false,
                cellStyles: true,
                bookImages: true
            });
        }
        const errorMsg = values.find(x => x.error != undefined);
        if(errorMsg && errorMsg.error){
            const msg = errorMsg.error.message.toLowerCase();
            if(msg.includes('unknown data')) exportResult('일부 프로젝트의\n선택한 기간의 데이터를 찾을 수 없습니다');
        }
        else exportResult('완료');
    }).catch(err => {
        console.log(err);
        exportResult('파일 저장 실패');
    });

    function exportResult(msg) {
        setTimeout(() => {
            $('#kpiExportDlg').modal('hide');

            $('#kpiExportResultDlg #message').html(msg);
            $('#kpiExportResultDlg').modal('show');

            $('#kpi-export-chart').empty();
            $('#kpi-export-os-chart').empty();
        }, 1000);
    }
});

$(document).on('shown.bs.modal', '#kpiExportDlg', () => {
    const lastDay = moment().subtract(1, 'day').subtract(10, 'hours').subtract(30, 'minutes');
    const beginDate = moment(lastDay).subtract(1, 'month').format('YYYY-MM-DD');
    const endDate = lastDay.format('YYYY-MM-DD');

    $('#exportBeginDate').val(beginDate);
    $('#exportEndDate').val(endDate);

    $('#exportBeginDate').datetimepicker({
        format: 'YYYY-MM-DD',
        date: new Date(beginDate),
        maxDate: new Date(endDate),
    });
    $('#exportEndDate').datetimepicker({
        format: 'YYYY-MM-DD',
        date: new Date(endDate),
        maxDate: new Date(endDate),
    });
});

//현재 화면 내용 내려받기 기능
$(document).on('click', '#kpiExportCurrentPage', () => {
    const tabs = $('#kpiTab').find('a');
    let active_id = null;
    tabs.each(function() {
        if($(this).hasClass('active')){
            active_id = $(this).attr('id');
        }
    });
    if(!active_id) return;

    exportReport(active_id);
});

//서버 데이터 선택에 의한 리포트 작성 처리
function exportReports(active_id, _data, canvases = undefined, app = undefined){
    let sheet = null;
    let sub_fileName = '';
    let isHeaderFormatExist = true;

    switch(active_id){
        //kpi-report-tab : (종합)지표현황
        //kpi-analytics-tab : KPI 현황 (NRU, DAU, ARPPU 등...)
        //kpi-ad-tab : 광고매출현황
        //kpi-daily-report-tab : 일일 지표현황
        //kpi-daily-tab : 일일매출
        case 'kpi-report-tab':
            var report_title = '지표 현황';

            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : report_title,
                data : _data,
                appName : app
            });

            if(canvases.canvas){
                sheet = insertChartImageToSheet(_data, sheet, canvases.canvas);
            }

            if(canvases.osCanvas){
                sheet = genOSWeeklyRevenueReport(_data, sheet, canvases.osCanvas, { c : 2, r : 53 }, { x : 45, y : 1180 });
            }

            sub_fileName = report_title + '_지표현황';
            break;
        case 'kpi-daily-tab':
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : '',
                data : _data
            });
            sheet = genDailyTotalReport(_data, sheet);
            sheet = appendChapterHeader('C4', '일일매출 현황', sheet);
            // sheet = XLSX.utils.json_to_sheet(data, {skipHeader : true});

            sub_fileName = '일일매출';
            break;
        case 'kpi-analytics-tab':
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : 'KPI 현황',
                data : _data
            });
            sheet = genKPIReportSheet(_data, sheet);

            var header = $('#kpi-analytics-tab').text().replace('KPI', '').replaceAll('"', '').trim();
            sheet = appendChapterHeader('C4', 'KPI 현황', sheet);
            sub_fileName = header + '_KPI 현황';
            isHeaderFormatExist = false;
            break;
        case 'kpi-ad-tab':
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : '',
                data : _data
            });
            sheet = genADReportSheet(_data, sheet);

            var header = $('#kpi-ad-tab').text().replaceAll('"', '').trim();
            sheet = appendChapterHeader('C4', 'KPI 현황', sheet);
            sub_fileName = header;
            break;
        case 'kpi-daily-report-tab':
            //일일지표현황
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : 'SUPERSTAR DAILY 지표현황',
                data : _data
            });
            sheet = genDailyReport(_data, sheet);
            sheet = appendChapterHeader('C8', '일간 프로젝트별 매출', sheet);
            
            const header_begin = { r : 9, c : 2 };
            sheet['T8']  = {
                t: 's', v: '* 환율 정보는 전날 23시55분(한국시간 기준) investing.com에서 가져오고 있습니다.',
                s: { sz: '10', alignment: { horizontal: 'left' }}
            };

            const ref_begin_issue_detail_range = { r : header_begin.r + _data.length + 1, c : header_begin.c };
            sheet = genExportVersionDailyReportSheet(_data, sheet, ref_begin_issue_detail_range);
            sub_fileName = '일일지표현황';
            break;
    }

    const totalRange = XLSX.utils.decode_range(sheet['!ref']);
    if(isHeaderFormatExist) totalRange.s = { c : 0, r : 8 };
    XLSX.utils.sheet_set_range_style(sheet, totalRange, {
        name : 'Malgun Gothic',
        sz : 8
    });
    
    return sheet;
}

//현재 화면 내용 내려받기에 의한 리포트 작성 처리
function exportReport(active_id){
    let sheet = null;
    let sub_fileName = '';
    let sheet_name = '';

    const app = $('#selectProjectButton').attr('data-value');
    const isTotal = app === 'TOTAL';

    let isHeaderFormatExist = true;
    //하나의 통합 sheet를 제작할 때 유동적으로 cell 시작점을 변하게 하기 위해 사용하는 변수
    let sheet_write_index = { r : 2, c : 2 };

    switch(active_id){
        //kpi-report-tab : (종합)지표현황
        //kpi-analytics-tab : KPI 현황 (NRU, DAU, ARPPU 등...)
        //kpi-ad-tab : 광고매출현황
        //kpi-daily-report-tab : 일일 지표현황
        //kpi-daily-tab : 일일매출
        case 'kpi-report-tab':
            sheet_name = `${app}_Report`;
            var report_title = $('#report-title').text();

            var spans = $('#kpi-weekly-report thead').first().find('span');
            let headers = [[]];
            spans.each(function(index, item){
                headers[0].push(item.textContent);
            });

            var data = headers.concat(weeklyReportTable.getData());

            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : report_title,
                data : data
            });

            const canvas = $('#report-chart')[0];
            const osCanvas = $('#week-revenue-chart')[0];

            sheet = insertChartImageToSheet(data, sheet, canvas);
            sheet_write_index.r = 26;
            
            sheet = genWeeklyReportSheet(data, sheet, sheet_write_index);

            if(!isTotal){
                //total이 아닌경우 (ss 포함)
                
                var spans = $('#kpi-monthly-report thead').first().find('span');
                headers = [[]];
                spans.each(function(index, item){
                    headers[0].push(item.textContent);
                });
                data = headers.concat(monthlyShortReportTable.getData());

                if(app === 'SS'){
                    sheet = genMonthly_small_ReportSheet(data, sheet, { r : sheet_write_index.r + 1, c: sheet_write_index.c + 14 });
                    sheet = genOSWeeklyRevenueReport(data, sheet, osCanvas, sheet_write_index, { x : 45, y : 1400 });
                }
                else if(app === 'SS_JP'){
                    sheet = genMonthly_small_ReportSheet(data, sheet, { r : sheet_write_index.r + 1, c: sheet_write_index.c + 14 });
                    sheet = genOSWeeklyRevenueReport(data, sheet, osCanvas, sheet_write_index, { x : 45, y : 1280 });
                }
                else{
                    sheet = genMonthly_small_ReportSheet(data, sheet, { r : sheet_write_index.r + 1, c: sheet_write_index.c + 14 });
                    sheet = genOSWeeklyRevenueReport(data, sheet, osCanvas, sheet_write_index, { x : 45, y : 1290 });
                }
            }
            else{
                //total인 경우
                var spans = $('#kpi-weekly-project thead').first().find('span');
                let weeklyProjectHeaders = [[]];
                spans.each(function(index, item){
                    weeklyProjectHeaders[0].push(item.textContent);
                });

                spans = $('#kpi-total-monthly-report thead').first().find('span');
                let monthlyReportHeaders = [[]];
                spans.each(function(index, item){
                    monthlyReportHeaders[0].push(item.textContent);
                });

                var summary_leftData = weeklySummaryLeftTable.getData();
                var summary_rightData = weeklySummaryRightTable.getData();

                var weeklyProjectData = weeklyProjectHeaders.concat(weeklyProjectTable.getData());
                var monthlyReportData = monthlyReportHeaders.concat(monthlyReportTable.getData());

                sheet = genWeeklySummearySheet(
                    summary_leftData, 
                    summary_rightData, 
                    sheet, 
                    sheet_write_index
                );

                sheet = genWeeklyProjectSheet(
                    weeklyProjectData, 
                    sheet, 
                    sheet_write_index
                );

                sheet = genMonthly_large_ReportSheet(
                    monthlyReportData, 
                    sheet, 
                    sheet_write_index
                );
            }

            sheet = appendChapterHeader('C26', '종합 지표 현황', sheet);

            sub_fileName = $('#searchDate').val() + '_' + report_title;
            break;
        case 'kpi-daily-tab':
            //data 소숫점 반올림
            sheet_name = `${app}_Daily`;
            var _dailyTableData = dailyTable.getData();

            var _headers = [[]];
            _headers[0] = dailyOptions.colHeaders;
            var _data = _headers.concat(_dailyTableData);
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : '',
                data : _data
            });
            sheet = genDailyTotalReport(_data, sheet);
            sheet = appendChapterHeader('C4', '일일매출 현황', sheet);

            var beginDate = $('#beginDate').val();
            var endDate = $('#endDate').val();

            sub_fileName = `${beginDate}_${endDate}_${app}_일일매출`;
            break;
        case 'kpi-analytics-tab':
            sheet_name = `${app}_KPI`;
            var data = analyticsOptions.nestedHeaders.concat(analyticsTable.getData());
            
            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : 'KPI 현황',
                data : data
            });
            sheet = genKPIReportSheet(data, sheet);
            sheet = appendChapterHeader('C4', 'KPI 현황', sheet);

            var beginDate = $('#beginDate').val();
            var endDate = $('#endDate').val();

            sub_fileName = `${beginDate}_${endDate}_${app}_KPI현황`;

            isHeaderFormatExist = false;
            break;
        case 'kpi-ad-tab':
            sheet_name = `${app}_AD`;
            const kpi_ad_nestedHeaders = [['Date', '[USD]Tapjoy', '[KRW]Tapjoy', '[USD]ADColony', '[KRW]ADColony', '[USD]Admob', '[KRW]Admob', '[USD]AdGeneration', '[KRW]AdGeneration', 'AD COUNT', '환율[1$]', '환율[100¥]']];
            var adTableData = adTable.getData();
            adTableData.forEach(function(val, index){
                for(let step = 0; step < val.length; step++){
                    const target_val = adTableData[index][step];
                    if(target_val && !isNaN(target_val)) adTableData[index][step] = target_val.toFixed(2);
                }
            });
            var data = kpi_ad_nestedHeaders.concat(adTableData);

            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : '',
                data : data
            });
            sheet = genADReportSheet(data, sheet);
            sheet = appendChapterHeader('C4', '광고매출 현황', sheet);

            var beginDate = $('#beginDate').val();
            var endDate = $('#endDate').val();

            sub_fileName = `${beginDate}_${endDate}_${app}_광고매출`;
            break;
        case 'kpi-daily-report-tab':
            //일일지표현황
            sheet_name = `${app}_Daily_Report`;
            var _dailyReportTable = dailyReportTable.getData();
            var data = dailyReportOptions.nestedHeaders.concat(_dailyReportTable);

            sheet = initSingleReportTemplate({
                type : active_id, 
                sheet : sheet,
                headerText : 'SUPERSTAR DAILY 지표현황',
                data : data
            });
            sheet = genDailyReport(data, sheet);
            sheet = appendChapterHeader('C8', '일간 프로젝트별 매출', sheet);

            const header_begin = { r : 9, c : 2 };
            sheet['T8']  = {
                t: 's', v: '* 환율 정보는 전날 23시55분(한국시간 기준) investing.com에서 가져오고 있습니다.',
                s: { sz: '10', alignment: { horizontal: 'left' }}
            };

            //이슈 상세 내용[DailyReportKeyCommentDetail] 추가
            const ref_begin_issue_detail_range = { r : header_begin.r + data.length + 1, c : header_begin.c };
            sheet = genExportVersionDailyReportSheet(data, sheet, ref_begin_issue_detail_range);
            
            var searchDate = $('#searchDate').val();
            sub_fileName = `${searchDate}_일일지표현황`;
            break;
    }

    if(active_id != 'kpi-report-tab'){
        const totalRange = XLSX.utils.decode_range(sheet['!ref']);
        if(isHeaderFormatExist) totalRange.s = { c : 0, r : 8 };
        XLSX.utils.sheet_set_range_style(sheet, totalRange, {
            name : 'Malgun Gothic',
            sz : 11
        });
    }
    else{
        const totalRange = XLSX.utils.decode_range(sheet['!ref']);
        if(isHeaderFormatExist) totalRange.s = { c : 0, r : 8 };
        XLSX.utils.sheet_set_range_style(sheet, totalRange, {
            name : 'Malgun Gothic'
        });
    }

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, sheet, sheet_name);
    XLSX.writeFile(wb, sub_fileName + '.xlsx', {
        bookType: 'xlsx',
        bookSST: false,
        type: 'binary',
        showGridLines: false,
        cellStyles: true,
        bookImages: true
    });
}

//[다운로드용] 일일지표현황 시트 생성
//여기에는 comment 내용까지 담김 (View에서 보여지는건 handsontable에 comment가 담기지 않음)
function genExportVersionDailyReportSheet(data, sheet, begin_range){
    const range = XLSX.utils.decode_range(sheet['!ref']);
    range.e.r += data.length + 10;

    const detail_parent = $('#kpi-daily-issue');
    const left_context = detail_parent.find('#detail-left').val();
    const right_context = detail_parent.find('#detail-right').val();

    const left_context_cell = XLSX.utils.encode_cell(begin_range);
    const right_context_cell = XLSX.utils.encode_cell({ r : begin_range.r, c : begin_range.c + 14 });

    const left_context_cell_header = XLSX.utils.encode_cell({ r : begin_range.r - 1, c : begin_range.c });
    
    sheet[right_context_cell] = { t: 's', v: right_context, s: {
        sz: '10',
        alignment: { vertical: 'top', horizontal : 'left', wrapText : true }
    }};
    sheet[left_context_cell] = { t: 's', v: left_context, s: {
        sz: '10',
        alignment: { vertical: 'top', horizontal : 'left', wrapText : true }
    }};

    const target_date = data[0][3].split('(')[0];
    const lastDay = moment(target_date, 'YY/MM/DD', true).format('MM/DD');
    sheet[left_context_cell_header] = { t: 's', v: `■ ${lastDay} 이슈`, s: {
        sz: '10',
        alignment: { vertical: 'top', horizontal: 'left' }
    }};

    const newMerges = [
        {
            s : begin_range,
            e : { c : begin_range.c + 13, r : begin_range.r + 10 }
        },
        {
            s : { c : begin_range.c + 14, r : begin_range.r },
            e : { c : begin_range.c + 28, r : begin_range.r + 10 }
        }
    ];

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...newMerges);

    const left_cell_range = XLSX.utils.encode_cell(newMerges[0].s) + ':' + XLSX.utils.encode_cell(newMerges[0].e);
    const right_cell_range = XLSX.utils.encode_cell(newMerges[1].s) + ':' + XLSX.utils.encode_cell(newMerges[1].e);

    XLSX.utils.sheet_set_range_style(sheet, left_cell_range, {
        top: { style: 'double' , color : {rgb : '#dedede'} }, bottom: { style: 'double', color : {rgb : '#dedede'} },
        left: { style: 'double', color : {rgb : '#f0dededef0f0'} }, right: { style: 'double', color : {rgb : '#dedede'} }
    });

    XLSX.utils.sheet_set_range_style(sheet, right_cell_range, {
        top: { style: 'double' , color : {rgb : '#dedede'} }, bottom: { style: 'double', color : {rgb : '#dedede'} },
        left: { style: 'double', color : {rgb : '#f0dededef0f0'} }, right: { style: 'double', color : {rgb : '#dedede'} }
    });

    sheet['!ref'] = XLSX.utils.encode_range(range);

    return sheet;
}

//지표 현황에서 데이터 시작 구간
const kpi_report_data_begin = { c : 2, r : 26 };
const kpi_analytics_data_begin = { c : 2, r: 4 };
const kpi_daily_report_data_begin = { c : 2, r : 8 }

function insertChartImageToSheet(data, sheet, canvas){
    const range = XLSX.utils.decode_range(sheet['!ref']);

    range.e.r += data.length + kpi_report_data_begin.r;

    sheet['!ref'] = XLSX.utils.encode_range(range);
    var picSrc = canvas.toDataURL("image/png");
    var picBlob = picSrc.split(",")[1];

    if(picBlob !== undefined){
        if(!sheet['!images']) sheet['!images'] = [];
        sheet['!images'].push(
            {
                '!pos' : { x:30, y:140, w: 1200, h:400 },
                '!data' : picBlob,
                '!datatype' : 'base64',
            }
        );
    }
    
    return sheet;
}

function add_cell_to_sheet(worksheet, address, value) {
	/* cell object */
	var cell = {t:'?', v:value};

	/* assign type */
	if(typeof value == "string") cell.t = 's'; // string
	else if(typeof value == "number") cell.t = 'n'; // number
	else if(value === true || value === false) cell.t = 'b'; // boolean
	else if(value instanceof Date) cell.t = 'd';
	else throw new Error("cannot store value");

	/* add to worksheet, overwriting a cell if it exists */
	worksheet[address] = cell;

	/* find the cell range */
	var range = XLSX.utils.decode_range(worksheet['!ref']);
	var addr = XLSX.utils.decode_cell(address);

	/* extend the range to include the new cell */
	if(range.s.c > addr.c) range.s.c = addr.c;
	if(range.s.r > addr.r) range.s.r = addr.r;
	if(range.e.c < addr.c) range.e.c = addr.c;
	if(range.e.r < addr.r) range.e.r = addr.r;

	/* update range */
	worksheet['!ref'] = XLSX.utils.encode_range(range);
}

//소제목 추가
function appendChapterHeader(target_row, value, sheet){
    sheet[target_row]  = {
        t: 's', v: '■ ' + value,
        s: { sz: '10', alignment: { horizontal: 'left' }}
    };
    return sheet;
}

//[현재화면 내려받기] 추출용 지표 현황 -> 주간 요약 시트 생성
function genWeeklySummearySheet(leftData, rightData, sheet, begin_range){
    if(leftData === undefined || leftData.length === 0 || rightData === undefined || rightData.length === 0) return sheet;

    var data_begin_range = { c : begin_range.c, r : begin_range.r };

    const header_cell_ref = XLSX.utils.encode_cell(
        { 
            r : data_begin_range.r - 1, 
            c : data_begin_range.c 
        }
    );

    let left_table_merges = [];
    let right_table_merges = [];
    const right_table_begin_col = 12;

    right_table_merges.push(
        {
            s : { c : right_table_begin_col, r : data_begin_range.r },
            e : { c : right_table_begin_col + 1, r : data_begin_range.r + rightData.length - 1}
        },
        {
            s : { c : right_table_begin_col + 2, r : data_begin_range.r },
            e : { c : right_table_begin_col + 3, r : data_begin_range.r + rightData.length - 1}
        }
    );

    left_table_merges.push(
        {
            s : { c : data_begin_range.c, r : data_begin_range.r },
            e : { c : data_begin_range.c + 1, r : data_begin_range.r }
        },
        {
            s : { c : data_begin_range.c + 2, r : data_begin_range.r },
            e : { c : data_begin_range.c + 4, r : data_begin_range.r }
        },
        {
            s : { c : data_begin_range.c + 5, r : data_begin_range.r },
            e : { c : data_begin_range.c + 6, r : data_begin_range.r }
        },
        {
            s : { c : data_begin_range.c + 7, r : data_begin_range.r },
            e : { c : data_begin_range.c + 8, r : data_begin_range.r }
        },
        {
            s : { c : data_begin_range.c, r : data_begin_range.r + 1 },
            e : { c : data_begin_range.c + 1, r : data_begin_range.r + 1 }
        },
        {
            s : { c : data_begin_range.c + 2, r : data_begin_range.r + 1 },
            e : { c : data_begin_range.c + 4, r : data_begin_range.r + 1 }
        },
        {
            s : { c : data_begin_range.c + 5, r : data_begin_range.r + 1 },
            e : { c : data_begin_range.c + 6, r : data_begin_range.r + 1 }
        },
        {
            s : { c : data_begin_range.c + 7, r : data_begin_range.r + 1 },
            e : { c : data_begin_range.c + 8, r : data_begin_range.r + 1 }
        }
    );
    
    begin_range.r += rightData.length + 2;

    let arpu_index = { c : right_table_begin_col + 4, r : data_begin_range.r };
    let canMerge = false;
    
    XLSX.utils.sheet_set_range_style(sheet, right_table_merges[0], {
        sz: '8',
        right: { style: 'thin' },
        left: { style: 'thin' },
        bottom : { style: 'thin' },
        top : { style: 'thin' }
    });

    XLSX.utils.sheet_set_range_style(sheet, right_table_merges[1], {
        sz: '8',
        right: { style: 'thin' },
        left: { style: 'thin' },
        bottom : { style: 'thin' },
        top : { style: 'thin' }
    });

    const right_header_text_ref = XLSX.utils.encode_cell(right_table_merges[0].s);
    sheet[right_header_text_ref] = {
        t: 's', v: rightData[0][0],
        s: { 
            sz: '8',
            alignment: { horizontal: 'left', vertical: 'center' },
            right: { style: 'thin' },
            left: { style: 'thin' },
            bottom : { style: 'thin' },
            top : { style: 'thin' },
            fgColor : {rgb : '0xEFEFEF'}
        }
    };
    const right_header_val_ref = XLSX.utils.encode_cell(right_table_merges[1].s);
    sheet[right_header_val_ref] = {
        t: 's', v: rightData[0][1] != null ? parseInt(rightData[0][1]).toFixed(1) : '',
        s: { 
            sz: '8',
            alignment: { horizontal: 'right', vertical: 'center' },
            right: { style: 'thin' },
            left: { style: 'thin' },
            bottom : { style: 'thin' },
            top : { style: 'thin' }
        }
    };

    let index = 2;
    for(let row=0; row<rightData.length; row++){
        for(let col=0; col<5*2; col++){
            if(row > 0){
                if(index === 0) index = 2;
            }
            if(canMerge){
                const merge = {
                    s : { c : arpu_index.c, r : arpu_index.r },
                    e : { c : arpu_index.c + 1, r : arpu_index.r }
                }
                right_table_merges.push(merge);

                if(rightData[row][index - 1]){
                    const cell_ref = XLSX.utils.encode_cell({c : arpu_index.c, r : arpu_index.r});
                    sheet[cell_ref] = {
                        t: 's', v: rightData[row][index - 1].toFixed(1)
                    }
                }

                XLSX.utils.sheet_set_range_style(sheet, merge, {
                    sz: '8',
                    alignment: { horizontal: 'right', vertical: 'center' },
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' }
                });

                arpu_index.c += 2;
            }
            else{
                const cell_ref = XLSX.utils.encode_cell({c : arpu_index.c, r : arpu_index.r});
                sheet[cell_ref] = {
                    t: 's', v: rightData[row][index] != null ? rightData[row][index] : '',
                    s: { 
                        sz: '8',
                        alignment: { horizontal: 'left', vertical: 'center' },
                        right: { style: 'thin' },
                        left: { style: 'thin' },
                        bottom : { style: 'thin' },
                        top : { style: 'thin' },
                        fgColor : {rgb : '0xEFEFEF'}
                    }
                }
                index += 2;
                arpu_index.c += 1;
            }
            canMerge = !canMerge;
        }
        index = 0;
        arpu_index.c = right_table_begin_col + 4;
        arpu_index.r += 1;
    }

    let merge_index = 0;
    for(let row=0; row<leftData.length; row++){
        for(let col=0; col<4; col++){
            const cell_ref = XLSX.utils.encode_cell(left_table_merges[merge_index].s);
            let filtered_val = leftData[row][col] != null ? leftData[row][col] : '';
            if(col == 1){
                if(!isNaN(filtered_val)){
                    filtered_val = Math.floor(filtered_val).toLocaleString();
                }
            }
            else if(col == 3){
                filtered_val = (filtered_val * 100).toFixed(2) + '%';
            }

            if(col % 2 === 0){
                sheet[cell_ref] = {
                    t: 's', v: filtered_val,
                    s: { 
                        sz: '8',
                        alignment: { horizontal: 'left', vertical: 'center' },
                        fgColor : {rgb : '0xEFEFEF'}
                    }
                }
            }
            else{
                sheet[cell_ref] = {
                    t: 's', v: filtered_val,
                    s: { 
                        sz: '8',
                        alignment: { horizontal: 'right', vertical: 'center' }
                    }
                }
            }
            XLSX.utils.sheet_set_range_style(sheet, left_table_merges[merge_index], {
                sz: '8',
                right: { style: 'thin' },
                left: { style: 'thin' },
                bottom : { style: 'thin' },
                top : { style: 'thin' }
            });
            merge_index++;
        }
    }

    sheet[header_cell_ref]  = {
        t: 's', v: '■ 주간 요약',
        s: { sz: '10', alignment: { horizontal: 'left' }}
    };

    //TODO 리뉴얼 이어서...
    sheet['!merges'].push(...right_table_merges);
    sheet['!merges'].push(...left_table_merges);
    return sheet;
}

//[현재화면 내려받기] 추출용 지표 현황 -> 주간 프로젝트별 매출 시트 생성
function genWeeklyProjectSheet(data, sheet, begin_range){
    if(data === undefined || data.length === 0) return sheet;

    var merges = [];

    const header_cell_ref = XLSX.utils.encode_cell({ r : begin_range.r - 1, c : 2 });
    sheet[header_cell_ref] =  { 
        t: 's', 
        v: '■ 주간 프로젝트 현황',
        s: { 
            sz: '10'
        }
    };

    var filed_range = { 
        s : { c : 2, r : begin_range.r },
        e : { c : 2 + (data[0].length - 1) * 3, r : begin_range.r + data.length } 
    };

    begin_range.r += data.length + 2;

    for(let row = filed_range.s.r; row < filed_range.e.r; row++){
        for(let col = filed_range.s.c; col < filed_range.e.c; col++){
            if(col === filed_range.s.c){
                merges.push(
                    {
                        s : { c : col, r : row },
                        e : { c : col + 1, r : row }
                    }
                );
            }
            else{
                if((col - 1) % 3 === 0){
                    merges.push(
                        {
                            s : { c : col, r : row },
                            e : { c : col + 2, r : row }
                        }
                    );
                }
            }
        }
    }

    var cell_index = 0;

    for(let row = 0; row < data.length; row++){
        for(let col = 0; col < data[0].length; col++){
            const target_cell = XLSX.utils.encode_cell({ r: merges[cell_index].s.r, c : merges[cell_index].s.c });

            var filtered_val = data[row][col] !== undefined ? data[row][col] : '';

            if(!isNaN(filtered_val)) filtered_val = Math.floor(filtered_val).toLocaleString();
            if(filtered_val !== '0'){
                sheet[target_cell] =  { t: 's', v: filtered_val, s: {
                        sz: '10'
                    }
                };
            }

            if(row === 0){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    fgColor : {rgb : '0xEFEFEF'},
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment: { horizontal: 'center' }
                });
            }
            else if(col === 0){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    fgColor : {rgb : '0xEFEFEF'},
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment: { horizontal: 'left' }
                });
            }
            else{
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment: { horizontal: 'right' }
                });
            }

            cell_index++;
        }
    }

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...merges);

    return sheet;
}

//[현재화면 내려받기] 추출용 월간지표 현황 시트 생성(TOTAL)
function genMonthly_large_ReportSheet(data, sheet, begin_range){
    if(data === undefined || data.length === 0) return sheet;

    var merges = [];

    const header_cell_ref = XLSX.utils.encode_cell({ r : begin_range.r - 1, c : 2 });
    sheet[header_cell_ref] =  { 
        t: 's', 
        v: '■ 월간 지표',
        s: { 
            sz: '10'
        }
    };

    var filed_range = { 
        s : { c : 2, r : begin_range.r },
        e : { c : 2 + (data[0].length - 1) * 3, r : begin_range.r + data.length } 
    };

    begin_range.r += data.length + 2;

    for(let row = filed_range.s.r; row < filed_range.e.r; row++){
        for(let col = filed_range.s.c; col < filed_range.e.c; col++){
            if(col === filed_range.s.c){
                merges.push(
                    {
                        s : { c : col, r : row },
                        e : { c : col + 1, r : row }
                    }
                );
            }
            else{
                if((col - 1) % 3 === 0){
                    merges.push(
                        {
                            s : { c : col, r : row },
                            e : { c : col + 2, r : row }
                        }
                    );
                }
            }
        }
    }

    var cell_index = 0;

    for(let row = 0; row < data.length; row++){
        for(let col = 0; col < data[0].length; col++){
            const target_cell = XLSX.utils.encode_cell({ r: merges[cell_index].s.r, c : merges[cell_index].s.c });

            var filtered_val = data[row][col] !== undefined ? data[row][col] : '';

            if(row !== 0 && col !== 0){
                if(data[0][col].includes('대비') || data[row][0].includes('비율')){
                    filtered_val = (filtered_val * 100).toFixed(2) + ' %';
                }
                else{
                    if(data[row][0].includes('ARPU/D')){
                        filtered_val = filtered_val.toFixed(2);
                    }
                    else {
                        filtered_val = Math.floor(filtered_val).toLocaleString();
                    }
                }
            }
            
            sheet[target_cell] =  { t: 's', v: filtered_val, s: {
                    sz: '10'
                }
            };

            if(row === 0){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    fgColor : {rgb : '0xEFEFEF'},
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment: { horizontal: 'center' }
                });
            }
            else if(col === 0){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    fgColor : {rgb : '0xEFEFEF'},
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment: { horizontal: 'left' }
                });
            }
            else{
                if(data[0][col].includes('대비')){
                    if(!filtered_val.includes('-')){
                        XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                            right: { style: 'thin' },
                            left: { style: 'thin' },
                            bottom : { style: 'thin' },
                            top : { style: 'thin' },
                            alignment: { horizontal: 'right' },
                            color : { rgb : 'FF0000'}
                        });
                    }
                    else{
                        XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                            right: { style: 'thin' },
                            left: { style: 'thin' },
                            bottom : { style: 'thin' },
                            top : { style: 'thin' },
                            alignment: { horizontal: 'right' },
                            color : { rgb : '0000FF'}
                        });
                    }
                }
                else{
                    XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                        right: { style: 'thin' },
                        left: { style: 'thin' },
                        bottom : { style: 'thin' },
                        top : { style: 'thin' },
                        alignment: { horizontal: 'right' }
                    });
                }
            }

            cell_index++;
        }
    }

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...merges);

    return sheet;
}

//[현재화면 내려받기] 추출용 월간지표 현황 시트 생성(PROJECT 단위)
function genMonthly_small_ReportSheet(data, sheet, begin_range){
    if(data === undefined || data.length === 0) return sheet;

    var merges = [];
    var data_begin_range = { c : begin_range.c, r : begin_range.r };

    var filed_range = { 
        s : { c : data_begin_range.c , r : data_begin_range.r },
        e : { c : data_begin_range.c + data[0].length * 3, r : data_begin_range.r + data.length } 
    };

    var col_index = data_begin_range.c;
    for(let row = filed_range.s.r; row < filed_range.e.r; row++){
        for(let col = filed_range.s.c; col < filed_range.e.c; col++){
            if(col === col_index){
                merges.push(
                    {
                        s : { c : col, r : row },
                        e : { c : col + 2, r : row }
                    }
                );
                col_index += 3;
            }
        }
        col_index = data_begin_range.c;
    }

    const weeklyReport_header_cell_ref = XLSX.utils.encode_cell(
        { 
            r : data_begin_range.r - 1, 
            c : data_begin_range.c 
        }
    );

    sheet[weeklyReport_header_cell_ref]  = {
        t: 's', v: '■ 월간 지표',
        s: { sz: '10', alignment: { horizontal: 'left' }}
    };

    var cell_index = 0;

    var target_col = 2;
    var target_row = [5, 11];

    for(let row = 0; row < data.length; row++){
        for(let col = 0; col < data[0].length; col++){
            const target_cell = XLSX.utils.encode_cell({ r: merges[cell_index].s.r, c : merges[cell_index].s.c });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';

            if(target_row.includes(row) || col === target_col){
                if(!isNaN(filtered_val) && filtered_val){
                    filtered_val *= 100;
                    filtered_val = filtered_val.toFixed(2);
                    filtered_val += ' %';
                }
            }
            else{
                if(!isNaN(filtered_val) && filtered_val){
                    filtered_val = Math
                        .round(filtered_val)
                        .toLocaleString();
                }
            }

            sheet[target_cell] =  { 
                t: 's', 
                v: filtered_val, 
                s: { sz: '10' }
            };

            if(row === 0 || col === 0){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '0xEFEFEF'},
                    alignment : { horizontal : 'center' },
                    bold : true
                });
            }
            else{
                if(data[0][col].includes('대비')){
                    if(filtered_val && !filtered_val.includes('-')){
                        XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                            right: { style: 'thin' },
                            left: { style: 'thin' },
                            bottom : { style: 'thin' },
                            top : { style: 'thin' },
                            alignment: { horizontal: 'right' },
                            color : { rgb : 'FF0000'}
                        });
                    }
                    else{
                        XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                            right: { style: 'thin' },
                            left: { style: 'thin' },
                            bottom : { style: 'thin' },
                            top : { style: 'thin' },
                            alignment: { horizontal: 'right' },
                            color : { rgb : '0000FF'}
                        });
                    }
                }
                else{
                    XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                        right: { style: 'thin' },
                        left: { style: 'thin' },
                        bottom : { style: 'thin' },
                        top : { style: 'thin' },
                        alignment : { horizontal : 'right' }
                    });
                }
            }

            cell_index++;
        }
    }

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...merges);

    return sheet;
}

//추출용 OS별 7일간 매출 현황 시트 생성
function genOSWeeklyRevenueReport(data, sheet, canvas, data_begin_range, image_pos){
    if(data === undefined || data.length === 0 || canvas === undefined) return sheet;

    const os_weekly_cell_ref = XLSX.utils.encode_cell(
        { 
            r : data_begin_range.r - 1, 
            c : data_begin_range.c 
        }
    );
    
    sheet[os_weekly_cell_ref] =  { 
        t: 's', 
        v: '■ OS별 7일간 매출 현황',
        s: { 
            sz: '8'
        }
    };
    
    var picSrc = canvas.toDataURL("image/png");
    var picBlob = picSrc.split(",")[1];

    if(picBlob !== undefined){
        if(!sheet['!images']) sheet['!images'] = [];

        sheet['!images'].push(
            {
                '!pos' : { x:image_pos.x, y:image_pos.y, w: 480, h:245 },
                '!data' : picBlob,
                '!datatype' : 'base64',
            }
        );
    }
    return sheet;
}


//추출용 주간지표 현황 시트 생성
function genWeeklyReportSheet(data, sheet, sheet_write_index){
    if(data === undefined || data.length === 0) return sheet;

    var merges = [];

    const header_cell_ref = XLSX.utils.encode_cell({ r : sheet_write_index.r - 1, c : 2 });
    sheet[header_cell_ref] =  { 
        t: 's', 
        v: '■ 주간 지표 현황',
        s: { 
            sz: '10'
        }
    };

    var filed_range = { 
        s : { c : 2, r : sheet_write_index.r },
        e : { c : 2 + (data[0].length - 1) * 3, r : sheet_write_index.r + data.length } 
    };

    for(let row = filed_range.s.r; row < filed_range.e.r; row++){
        for(let col = filed_range.s.c; col < filed_range.e.c; col++){
            if(col === filed_range.s.c){
                merges.push(
                    {
                        s : { c : col, r : row },
                        e : { c : col + 1, r : row }
                    }
                );
            }
            else{
                if((col - 1) % 3 === 0){
                    merges.push(
                        {
                            s : { c : col, r : row },
                            e : { c : col + 2, r : row }
                        }
                    );
                }
            }
        }
    }

    sheet_write_index.r += data.length + 1;
    const issue_begin_row = sheet_write_index.r;

    const issue_header_cell_ref = XLSX.utils.encode_cell({ r : issue_begin_row, c : 2 });
    
    var issue_merges = [];
    issue_merges.push(
        {
            s : { c : 2, r : issue_begin_row + 1 },
            e : { c : 10, r : issue_begin_row + 11 }
        },
        {
            s : { c : 11, r : issue_begin_row + 1 },
            e : { c : 19, r : issue_begin_row + 11 }
        },
        {
            s : { c : 20, r : issue_begin_row + 1 },
            e : { c : 30, r : issue_begin_row + 11 }
        }
    );

    merges.push(...issue_merges);
    sheet[issue_header_cell_ref] =  { 
        t: 's', 
        v: '■ 업데이트 이슈',
        s: { 
            sz: '8'
        }
    };

    sheet_write_index.r += 14;

    let issue_default_value_cell_ref = XLSX.utils.encode_cell(issue_merges[0].s);
    const detail_default_left_text = $('#kpi-update-issue-left textarea').val();
    sheet[issue_default_value_cell_ref] =  { t: 's', v: detail_default_left_text }

    issue_default_value_cell_ref = XLSX.utils.encode_cell(issue_merges[1].s);
    const detail_default_center_text = $('#kpi-update-issue-center textarea').val();
    sheet[issue_default_value_cell_ref] =  { t: 's', v: detail_default_center_text }

    issue_default_value_cell_ref = XLSX.utils.encode_cell(issue_merges[2].s);
    const detail_default_right_text = $('#kpi-update-issue-right textarea').val();
    sheet[issue_default_value_cell_ref] =  { t: 's', v: detail_default_right_text }

    issue_merges.forEach(val => {
        var cell_ref_begin = XLSX.utils.encode_cell(val.s);
        var cell_ref_end = XLSX.utils.encode_cell(val.e);

        XLSX.utils.sheet_set_range_style(sheet, cell_ref_begin + ':' + cell_ref_end, {
            top: { style: 'medium', color: { rgb: 0xDEDEDE } },
            bottom: { style: 'medium', color: { rgb: 0xDEDEDE } },
            left: { style: 'medium', color: { rgb: 0xDEDEDE } },
            right: { style: 'medium', color: { rgb: 0xDEDEDE } },
            alignment : { vertical : 'top', horizontal : 'left', wrapText : true }
        });
    });

    var cell_index = 0;

    for(let row = 0; row < data.length; row++){
        for(let col = 0; col < data[0].length; col++){
            const target_cell = XLSX.utils.encode_cell({ r: merges[cell_index].s.r, c : merges[cell_index].s.c });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';

            if(data[row][0] === 'PUR' && col > 0){
                if(filtered_val != ''){
                    filtered_val *= 100;
                    filtered_val = filtered_val.toFixed(2);
                    filtered_val += ' %';
                }
            }

            if(data[row][0] != 'PUR' && col > 0){
                if(!isNaN(filtered_val) && filtered_val != ''){
                    if(data[row][0] === 'ARPU (₩)'){
                        filtered_val = filtered_val.toFixed(2).toLocaleString();
                    }
                    else{
                        filtered_val = Math.floor(filtered_val).toLocaleString();
                    }
                }
            }

            sheet[target_cell] =  { t: 's', v: filtered_val, s: {
                    sz: '10'
                }
            };

            //헤더 영역
            if(merges[cell_index].s.r === kpi_report_data_begin.r || merges[cell_index].s.c === kpi_report_data_begin.c){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : { rgb : '#f0f0f0' },
                    alignment : { horizontal : 'center'},
                    bold : true
                });
            }
            else{
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment : { horizontal : 'right'}
                });
            }

            if(merges[cell_index].s.c === 4 && merges[cell_index].s.r >= kpi_report_data_begin.r + 1){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : { rgb : '#fff2cc' }
                });
            }
            cell_index++;
        }
    }

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...merges);

    return sheet;
}

//KPI 추출용 시트 생성
function genKPIReportSheet(data, sheet, isTotalDownload = false){
    if(data === undefined || data.length === 0) return sheet;

    for(let col = 0; col < data[0].length; col++){
        for(let row = 0; row < data.length; row++){
            const target_cell = !isTotalDownload ? XLSX.utils.encode_cell({ r : kpi_analytics_data_begin.r + row, c : kpi_analytics_data_begin.c + col }) : XLSX.utils.encode_cell({ r : kpi_analytics_data_begin.r + 5 + row, c : kpi_analytics_data_begin.c + col });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';

            if(data[0][col] == 'PUR' && row > 0){
                filtered_val *= 100;
            }

            if(data[0][col] == '광고비율' && row > 0){
                filtered_val *= 100;
            }

            if(!isNaN(filtered_val) && data[0][col] != '업데이트' && data[0][col] != '이슈'){
                if(filtered_val != '') {
                    filtered_val = parseFloat(filtered_val).toFixed(2);
                    filtered_val = parseFloat(filtered_val).toLocaleString();
                };
            }

            if(data[0][col] == 'PUR' && col > 0){
                filtered_val += ' %';
            }

            if(data[0][col] == '광고비율' && col > 0){
                filtered_val += ' %';
            }

            sheet[target_cell] =  { t: 's', v: filtered_val, s: { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    top: { style: 'thin' },
                    bottom : { style: 'thin' },
                }
            };

            if(col === 0 || row === 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '0xEFEFEF'}
                };
            }

            if(col === 3 && row > 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '#e2efda'}
                };
            }

            if(col >= 9 && col <= 12 && row > 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '#e2efda'}
                };
            }

            if(col == 15 && row > 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '#e2efda'}
                };
            }
        }
    }
    return sheet;
}

//광고매출 추출용 시트 생성
function genADReportSheet(data, sheet){
    if(data === undefined || data.length === 0) return sheet;

    for(let col = 0; col < data[0].length; col++){
        for(let row = 0; row < data.length; row++){
            const target_cell = XLSX.utils.encode_cell({ r : kpi_analytics_data_begin.r + row, c : kpi_analytics_data_begin.c + col });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';

            if(!isNaN(filtered_val)){
                if(filtered_val != '') {
                    filtered_val = parseFloat(filtered_val).toLocaleString();
                    if(data[0][col].includes('USD')){
                        filtered_val = '$ ' + filtered_val;
                    }
                    else if(data[0][col].includes('KRW')){
                        filtered_val = '₩ ' + filtered_val;
                    }
                    else if(data[0][col].includes('JPY')){
                        filtered_val = '¥ ' + filtered_val;
                    }
                }
            }

            sheet[target_cell] =  { t: 's', v: filtered_val, s: { 
                    sz: '10',
                    top: { style: 'thin' },
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                }
            };

            if(col === 0 || row === 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '0xEFEFEF'}
                };
            }
        }
    }
    return sheet;
}

//일일 지표현황 추출용 시트 생성
function genDailyReport(data, sheet){
    if(data === undefined || data.length === 0) return sheet;

    var merges = [];
    var data_begin_row = kpi_daily_report_data_begin.r;

    var filed_range = { 
        s : { c : 2, r : data_begin_row },
        e : { c : 2 + (data[0].length - 1) * 3, r : data_begin_row + data.length } 
    };

    for(let row = filed_range.s.r; row < filed_range.e.r; row++){
        for(let col = filed_range.s.c; col < filed_range.e.c; col++){
            if(col === filed_range.s.c){
                merges.push(
                    {
                        s : { c : col, r : row },
                        e : { c : col + 1, r : row }
                    }
                );
            }
            else{
                if((col - 1) % 3 === 0){
                    merges.push(
                        {
                            s : { c : col, r : row },
                            e : { c : col + 2, r : row }
                        }
                    );
                }
            }
        }
    }

    var cell_index = 0;
    for(let row = 0; row < data.length; row++){
        for(let col = 0; col < data[0].length; col++){
            const target_cell = XLSX.utils.encode_cell({ r: merges[cell_index].s.r, c : merges[cell_index].s.c });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';
            
            if(!isNaN(filtered_val) && row > 1){
                if(filtered_val != '') filtered_val = Math.round(filtered_val).toLocaleString();
            }
            sheet[target_cell] =  { t: 's', v: filtered_val, s: {
                    sz: '10'
                }
            };

            const isTotalRow = data[row][0] === '합계' && col > 0;
            const isIssueRow = data[row][0].includes('이슈');

            //헤더 영역
            if(merges[cell_index].s.r === kpi_daily_report_data_begin.r || merges[cell_index].s.c === kpi_daily_report_data_begin.c){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : { rgb : '#f0f0f0' },
                    alignment : { horizontal : 'center'},
                    bold : true
                });
            }
            else{
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    alignment : { horizontal : 'right' }
                });
            }

            if(merges[cell_index].s.c === 4 && merges[cell_index].s.r >= kpi_daily_report_data_begin.r + 1){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : { rgb : '#fff2cc' }
                });
            }

            if(isTotalRow){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : { rgb : '#ccc0da' },
                    bold : true
                });
            }

            if(isIssueRow){
                XLSX.utils.sheet_set_range_style(sheet, merges[cell_index], {
                    alignment : {horizontal : 'center'}
                });
            }
            cell_index++;
        }
    }

    if(!sheet['!merges']) sheet['!merges'] = [];
    sheet['!merges'].push(...merges);

    return sheet;
}

//일일 매출 추출용 시트 생성
function genDailyTotalReport(data, sheet){
    if(data === undefined || data.length === 0) return sheet;

    for(let col = 0; col < data[0].length; col++){
        for(let row = 0; row < data.length; row++){
            const target_cell = XLSX.utils.encode_cell({ r : kpi_analytics_data_begin.r + row, c : kpi_analytics_data_begin.c + col });

            var filtered_val = data[row][col] != undefined ? data[row][col] : '';

            if(!isNaN(filtered_val)){
                if(filtered_val != '') {
                    filtered_val = parseFloat(filtered_val).toFixed(2);
                    filtered_val = parseFloat(filtered_val).toLocaleString();
                }
            }

            sheet[target_cell] =  { t: 's', v: filtered_val, s: { 
                    sz: '10',
                    top: { style: 'thin' },
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                }
            };

            if(col === 0 || row === 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '0xEFEFEF'}
                };
            }
            if(data[0][col].includes('_AD') && row > 0){
                sheet[target_cell].s = { 
                    sz: '10',
                    right: { style: 'thin' },
                    left: { style: 'thin' },
                    bottom : { style: 'thin' },
                    top : { style: 'thin' },
                    fgColor : {rgb : '#e2efda'}
                };
            }
        }
    }
    return sheet;
}

function genTotalSheet(){
    const sheet = XLSX.utils.json_to_sheet(data, {skipHeader : true});

    const range = XLSX.utils.decode_range(sheet['!ref']);
    const begin = range.e.r + 1;

    range.e.r += data.length + 3;
    sheet['!ref'] = XLSX.utils.encode_range(range);

    const titleCell = XLSX.utils.encode_cell({ c: 2, r: begin });
    sheet[titleCell] = { t: 's', v: title, s: { sz: '10' }};
    sheet['!rows'][begin] = { hpt: 15 };

    for (let col = 0; col < 10; col++) {
        const c = col ? ((col - 1) * 3 + 4) : 2;
        const c2 = c + (col ? 2 : 1);

        // 일간 프로젝트별 매출 해더
        const cell_ref = XLSX.utils.encode_cell({ c, r: begin + 1 });
        const cell_range = XLSX.utils.encode_range({
            s: { c, r: begin + 1 },
            e: { c: c2, r: begin + 1 }
        });
        sheet[cell_ref] = {
            t: 's',
            v: headerNames[col] !== undefined ? headerNames[col] : '',
            s: { sz: '10' }
        };
        XLSX.utils.sheet_set_range_style(sheet, cell_range, {
            fgColor: { rgb: 0xEFEFEF },
            top: { style: 'medium', color: { rgb: 0xDEDEDE } },
            bottom: { style: 'medium', color: { rgb: 0xDEDEDE } },
            left: { style: 'medium', color: { rgb: 0xDEDEDE } },
            right: { style: 'medium', color: { rgb: 0xDEDEDE } },
            alignment: { horizontal: 'center' }
        });
        sheet['!merges'].push({
            s: { c, r: begin + 1 },
            e: { c: c2, r: begin + 1 }
        });
        if (col === 0) {
            sheet['!rows'][begin + 1] = { hpt: 15 };
        }

        // 일간 프로젝트별 매출 내용
        data.forEach((value, row) => {
            const r = begin + row + 2;
            const cell_range = {
                s: { c, r },
                e: { c: c2, r }
            };
            const cell_ref = XLSX.utils.encode_cell(cell_range.s);
            const cell_range_ref = XLSX.utils.encode_range(cell_range);
            sheet[cell_ref] = { t: 's', v: value[header[col]], s: { sz: '10' }};
            if (col === 0) {
                XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                    fgColor: { rgb: 0xEFEFEF },
                    top: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    bottom: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    left: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    right: { style: 'medium', color: { rgb: 0xDEDEDE } },
                    alignment: { horizontal: 'center' }
                });
            } else {
                if (value[header[col]] === 0 || !isNaN(value[header[col]])) {
                    sheet[cell_ref].t = 'n';
                    sheet[cell_ref].z = '#,##0';
                }
                if (col === 1) {
                    XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                        fgColor: { rgb: 0xFFF2CC },
                        top: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        bottom: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        left: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        right: { style: 'thin', color: { rgb: 0xDEDEDE } }
                    });
                } else {
                    XLSX.utils.sheet_set_range_style(sheet, cell_range_ref, {
                        top: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        bottom: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        left: { style: 'thin', color: { rgb: 0xDEDEDE } },
                        right: { style: 'thin', color: { rgb: 0xDEDEDE } }
                    });
                }
            }
            sheet['!merges'].push(cell_range);
            if (col === 0) {
                sheet['!rows'][r] = { hpt: 15 };
            }
        });
    }
    sheet['!rows'].push({ hpt: 15 });
}

$(document).on('hide.bs.modal', '#kpiExportDlg', () => {
    $('#exportBeginDate').prop('disabled', false);
    $('#exportEndDate').prop('disabled', false);

    $('#export_report_all').prop('disabled', false);
    $('.export_report_items').prop('disabled', false);

    $('#export_analytics_all').prop('disabled', false);
    $('.export_analytics_items').prop('disabled', false);

    $('#export_ad_all').prop('disabled', false);
    $('.export_ad_items').prop('disabled', false);

    $('#export_daily_report').prop('disabled', false);

    $('#export_daily').prop('disabled', false);

    $('#kpiExportDlg .spinner-border').remove();
    $('#kpiExportDlg .fa-times-circle').remove();
    $('#kpiExportDlg .fa-check-circle').remove();

    $('#kpiExportDlg #export').prop('disabled', false).removeClass('disabled');
});

$(document).on('hide.bs.modal', '#kpiExportResultDlg', () => {
});
