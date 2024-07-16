let reportChart = null;
let weekRevenueChart = null;
let weeklyReportTable = null;
let weeklySummaryLeftTable = null;
let weeklySummaryRightTable = null;
let weeklyProjectTable = null;
let monthlyReportTable = null;
let referenceTable = null;  //지표 용어 설명
let monthlyShortReportTable = null;
const reportChartOptions = {
    type: 'bar',
    data: {
        labels: [],
        datasets: [{
            type: 'bar',
            label: '매출',
            yAxisID: 'A',
            data: [],
            backgroundColor: 'rgba(54, 162, 235, 0.7)',
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 1
        },
        {
            type: 'line',
            label: 'DAU',
            yAxisID: 'B',
            data: [],
            backgroundColor: 'rgba(0, 0, 0, 0)',
            borderColor: 'rgba(197, 90, 17, 1)',
            borderWidth: 1
        }]
    },
    options: {
        legend: {
            position: 'bottom'
        },
        tooltips: {
            callbacks: {
                label: (tooltipItem, data) => {
                    var value = data.datasets[tooltipItem.datasetIndex].data[tooltipItem.index];
                    return typeof value === 'number' ? parseInt(value).toLocaleString() : value;
                }
            }
        },
        scales: {
            yAxes: [{
                id: 'A',
                display: true,
                position: 'left',
                ticks: {
                    beginAtZero: true,
                    userCallback: (value, index, values) => {
                        return typeof value === 'number' ? parseInt(value).toLocaleString() : value;
                    }
                }
            },
            {
                id: 'B',
                display: true,
                position: 'right',
                ticks: {
                    beginAtZero: true,
                    userCallback: function(value, index, values) {
                        return typeof value === 'number' ? value.toLocaleString() : value;
                    }
                }
            }]
        }
    }
};
const weekRevenueChartOptions = {
    type: 'bar',
    data: {
        labels: [],
        datasets: [{
            label: 'Android',
            data: [],
            backgroundColor: 'rgba(54, 82, 235, 0.7)',
            borderColor: 'rgba(54, 82, 235, 1)',
            borderWidth: 1
        },{
            label: 'iOS',
            data: [],
            backgroundColor: 'rgba(197, 72, 17, 0.7)',
            borderColor: 'rgba(197, 72, 17, 1)',
            borderWidth: 1
        }]
    },
    options: {
        tooltips: {
            mode: 'index',
            intersect: false,
            callbacks: {
                label: (tooltipItem, data) => {
                    var value = data.datasets[tooltipItem.datasetIndex].data[tooltipItem.index];
                    return typeof value === 'number' ? parseInt(value).toLocaleString() : value;
                }
            }
        },
        scales: {
            xAxes: [{
                stacked: true,
            }],
            yAxes: [{
                display: true,
                stacked: true,
                ticks: {
                    beginAtZero: true,
                    userCallback: (value, index, values) => {
                        return typeof value === 'number' ? parseInt(value).toLocaleString() : value;
                    }
                }
            }]
        }
    }
};
const weeklyReportOptions = {
    data: [],
    columns: [
        {
            data: 'item',
            type: 'text'
        },
        {
            data: 'curWeek',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'prevWeek',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '0',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '1',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '2',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '3',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '4',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '5',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '6',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        }
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [85, 105, 105, 95, 95, 95, 95, 95, 95, 95],
    colHeaders: [],
    fixedColumnsLeft: 1,
    licenseKey: 'non-commercial-and-evaluation'
};
const weeklySummaryLeftTableOptions = {
    data: [],
    columns: [
        {
            data: 'item',
            type: 'text'
        },
        {
            data: 'value',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'percentageText',
            type: 'text'
        },
        {
            data: 'percentage',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            }
        }
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [82, 90, 58, 62],
    mergeCells: [ ],
    customBorders: [],
    licenseKey: 'non-commercial-and-evaluation'
};
const weeklySummaryRightTableOptions = {
    data: [],
    columns: [
        {
            data: 'arpuTotalTitle',
            type: 'text',
            className: 'htMiddle'
        },
        {
            data: 'arpuTotal',
            type: 'numeric',
            className: 'htMiddle',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'arpuCol0Title',
            type: 'text'
        },
        {
            data: 'arpuCol0',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'arpuCol1Title',
            type: 'text'
        },
        {
            data: 'arpuCol1',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'arpuCol2Title',
            type: 'text'
        },
        {
            data: 'arpuCol2',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'arpuCol3Title',
            type: 'text'
        },
        {
            data: 'arpuCol3',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'arpuCol4Title',
            type: 'text'
        },
        {
            data: 'arpuCol4',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 1,
                    thousandSeparated: true
                }
            }
        },
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [65, 55, 51, 55, 51, 55, 51, 55, 51, 55, 51, 55],
    mergeCells: [],
    customBorders: [],
    licenseKey: 'non-commercial-and-evaluation'
};
const weeklyProjectOptions = {
    data: [],
    columns: [
        {
            data: 'item',
            type: 'text'
        },
        {
            data: 'curWeek',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: 'prevWeek',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '0',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '1',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '2',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '3',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '4',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '5',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '6',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        }
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [85, 105, 105, 95, 95, 95, 95, 95, 95, 95],
    colHeaders: [],
    licenseKey: 'non-commercial-and-evaluation',
    cells: (row, col) => ({
        wordWrap: false,
        renderer (instance, td, row, col, prop, value, cellProperties) {
            if (col === 0) {
                Handsontable.renderers.TextRenderer.apply(this, arguments);
                td.style.color = '#000';
                td.style.background = '#f0f0f0';
            } else {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (col === 1) {
                    td.style.background = '#fff2cc';
                }
            }
        }
    })
};
const monthlyReportOptions = {
    data: [],
    columns: [
        {
            data: 'item',
            type: 'text'
        },
        {
            data: '0',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: (row) => row[0] / row[1] - 1,
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            },
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (value < 0) {
                    td.style.color = '#00f';
                } else if (value > 0) {
                    td.style.color = '#f00';
                }
            }
        },
        {
            data: '1',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: (row) => row[1] / row[2] - 1,
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            },
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (value < 0) {
                    td.style.color = '#00f';
                } else if (value > 0) {
                    td.style.color = '#f00';
                }
            }
        },
        {
            data: '2',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: (row) => row[2] / row[3] - 1,
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            },
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (value < 0) {
                    td.style.color = '#00f';
                } else if (value > 0) {
                    td.style.color = '#f00';
                }
            }
        },
        {
            data: '3',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: (row) => row[3] / row[4] - 1,
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            },
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (value < 0) {
                    td.style.color = '#00f';
                } else if (value > 0) {
                    td.style.color = '#f00';
                }
            }
        },
        {
            data: '4',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [85, 105, 87, 105, 87, 105, 88, 105, 88, 105],
    colHeaders: [],
    fixedColumnsLeft: 1,
    licenseKey: 'non-commercial-and-evaluation'
};
const monthlyShortReportOptions = {
    data: [],
    columns: [
        {
            data: 'item',
            type: 'text'
        },
        {
            data: '0',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: (row) => row[0] / row[1] - 1,
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                }
            }
        },
        {
            data: '1',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        },
        {
            data: '2',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                }
            }
        }
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [80, 101, 81, 101, 101],
    colHeaders: [],
    licenseKey: 'non-commercial-and-evaluation'
};
const referenceTableOptions = {
    data: [],
    columns: [
        {
            data: '0',
            type: 'text'
        },
        {
            data: '1',
            type: 'text'
        }
    ],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [80, 180],
    colHeaders: [
        '항목', '설명'
    ],
    licenseKey: 'non-commercial-and-evaluation'
};

const itemMap = {
    'installs_Android': 'D/L',
    'installs_iOS': 'D/L',
    'NRU': 'NRU',
    'DAU': 'W/DAU',
    'PU': 'PU',
    'revenue_Android': '인앱매출 (₩)',
    'revenue_iOS': '인앱매출 (₩)',
    'AD': '광고매출 (₩)'
};
const itemOrder = {
    'D/L': 101,
    'W/DAU': 102,
    'MAU': 103,
    'NRU': 104,
    'PU': 105,
    'MPU': 106,
    'PUR': 107,
    'ARPU (₩)': 108,
    'ARPU/M (₩)': 109,
    'ARPU/D (₩)': 110,
    'ARPPU (₩)': 111,
    '인앱매출 (₩)': 112,
    '광고매출 (₩)': 113,
    '종합매출 (₩)': 114,
    '광고비율' : 115
};

const monthlyItemOrder = {
    'D/L': 101,
    'MAU': 102,
    'NRU': 104,
    'PU': 105,
    'MPU': 106,
    'PUR': 107,
    'ARPU (₩)': 108,
    'ARPU/M (₩)': 109,
    'ARPU/D (₩)': 110,
    'ARPPU (₩)': 111,
    '인앱매출 (₩)': 112,
    '광고매출 (₩)': 113,
    '종합매출 (₩)': 114,
    '광고비율' : 115
}

function updateReportChart(data) {
    const canvas = $('#report-chart');
    const rows = Object.values(data);

    reportChartOptions.data.labels = Object.keys(data).map(key => moment(key).format('YY/M/D'));
    reportChartOptions.data.datasets[0].data = rows.map(row => row.revenue);
    reportChartOptions.data.datasets[1].data = rows.map(row => row.DAU);

    if(reportChart){
        reportChart.clear();
        reportChart.stop();
    }

    if (!reportChart) {
        canvas.css({ height: 250 });
        reportChart = new Chart(canvas, reportChartOptions);
    } else {
		reportChart.update();
    }
}

function updateWeekRevenueChart(data) {
    const canvas = $('#week-revenue-chart');
    const searchDate = $('#searchDate').val();
    const rows = Object.values(data).reverse();

    weekRevenueChartOptions.data.labels = Object.keys(data).reverse().map(i => moment(searchDate).subtract(i, 'day').locale('kr').format('MM.DD(ddd)'));
    weekRevenueChartOptions.data.datasets[0].data = rows.map(row => row.android);
    weekRevenueChartOptions.data.datasets[1].data = rows.map(row => row.ios);

    if(weekRevenueChart){
        weekRevenueChart.clear();
        weekRevenueChart.stop();
    }

    if (!weekRevenueChart) {
        canvas.css({ height: 250 });
        weekRevenueChart = new Chart(canvas, weekRevenueChartOptions);
    } else {
		weekRevenueChart.update();
    }
}

function updateWeeklyReport(data) {
    const searchDate = $('#searchDate').val();

    weeklyReportOptions.data = Object.values(data).sort((a, b) => {
        // AD가 앞에..
        const [aName, aLocal] = a.item.replace(' 이슈', '').split('_');
        const [bName, bLocal] = b.item.replace(' 이슈', '').split('_');

        const aOrder = itemOrder[a.item] || ((aLocal ? 100 : 0) - projectOrder.indexOf(aName));
        const bOrder = itemOrder[b.item] || ((bLocal ? 100 : 0) - projectOrder.indexOf(bName));
        
        return aOrder - bOrder;
    });

    const beginRow = weeklyReportOptions.data.length - 10;
    const purRow = weeklyReportOptions.data.length - 6;
    const revenueRow = weeklyReportOptions.data.length - 3;

    const endRow = weeklyReportOptions.data.length - 1;
    console.log('endRow : ' + endRow);
    weeklyReportOptions.cells = (row, col) => {
        const cell = {
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                if (col === 0) {
                    Handsontable.renderers.TextRenderer.apply(this, arguments);
                    td.style.color = '#000';
                    td.style.background = '#f0f0f0';
                } else if (row < beginRow) {
                    Handsontable.renderers.TextRenderer.apply(this, arguments);
                    if (col > 2 && !value) {
                        td.innerHTML = '-';
                    }
                } else {
                    Handsontable.renderers.NumericRenderer.apply(this, arguments);
                    if (col === 1) {
                        td.style.background = '#fff2cc';
                    }
                }
                if(weeklyReportOptions.data[row] && (weeklyReportOptions.data[row].item.includes('ARPU'))){
                    cellProperties.numericFormat = {
                        pattern: '0.00'
                    };
                }
            }
        };
        if (row < beginRow) {
            cell.type = 'text';
        } else {
            cell.type = 'numeric';
            if (row === purRow) {
                cell.numericFormat = {
                    pattern: {
                        output: 'percent',
                        mantissa: 2,
                        trimMantissa: true,
                        spaceSeparated: true,
                    }
                };
            } else if (row >= revenueRow) {
                cell.numericFormat = {
                    pattern: {
                        mantissa: 0,
                        spaceSeparated: true,
                        thousandSeparated: true
                    },
                    culture: 'ko-KR',
                };
            }
        }
        return cell;
    };

    weeklyReportOptions.colHeaders = [];
    weeklyReportOptions.colHeaders.push('항목', '금주 Total', '지난주 Total');

    for (let i = 0; i < 7; i++) {
        weeklyReportOptions.colHeaders.push(moment(searchDate).weekday(i).locale('kr').format('YY/MM/DD(ddd)'));
    }

    if(weeklyReportTable){
        weeklyReportTable.removeHook();
    }
    else weeklyReportTable = new Handsontable(document.querySelector('#kpi-weekly-report'), weeklyReportOptions);
    
    weeklyReportTable.updateSettings(weeklyReportOptions);
}

function weeklySummaryReport(data) {
    weeklySummaryLeftTableOptions.data = [
        {
            item: '주간매출 (₩)',
            value: data.revenue,
            percentageText: '전주대비',
            percentage: data.revenueOverThePrevWeek
        },
        {
            item: 'WAU',
            value: data.WAU,
            percentageText: '전주대비',
            percentage: data.WAUOverThePrevWeek
        }
    ];

    weeklySummaryRightTableOptions.data = [];
    Object.keys(data).filter(key => projectOrder.indexOf(key.split('_')[0]) != -1).sort((a, b) => {
        const [aName, aLocal] = a.split('_');
        const [bName, bLocal] = b.split('_');

        const aOrder = (aLocal ? 100 : 0) - projectOrder.indexOf(aName);
        const bOrder = (bLocal ? 100 : 0) - projectOrder.indexOf(bName);
        
        return aOrder - bOrder;
    }).forEach((app, i) => {
        var _data = data[app];
        var arpu = _data.ARPU != undefined ? (_data.ARPU / _data.dateCount) : null;

        const row = Math.floor(i / 5);
        const col = i % 5;

        if(!weeklySummaryRightTableOptions.data[row]) weeklySummaryRightTableOptions.data.push([]);

        weeklySummaryRightTableOptions.data[row][`arpuCol${col}Title`] = app;
        weeklySummaryRightTableOptions.data[row][`arpuCol${col}`] = arpu;
    });

    weeklySummaryRightTableOptions.data[0]['arpuTotalTitle'] = '주간 ARPU\n(₩)';
    weeklySummaryRightTableOptions.data[0]['arpuTotal'] = data['ARPU'];

    weeklySummaryRightTableOptions.mergeCells = [
        { row : 0, col : 0, rowspan : weeklySummaryRightTableOptions.data.length, colspan : 1 },
        { row : 0, col : 1, rowspan : weeklySummaryRightTableOptions.data.length, colspan : 1 }
    ]

    weeklySummaryRightTableOptions.cells = (row, col) => {
        const cell = {
            type: 'text',
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if(col === 0){
                    td.style.whiteSpace = 'normal';
                }
                if(col % 2 != 0){
                    td.style.color = '#000';
                }
                else{
                    td.style.color = '#000';
                    td.style.background = '#f0f0f0';
                }
            }
        }
        return cell;
    }

    weeklySummaryLeftTableOptions.cells = (row, col) => {
        const cell = {
            type: 'text',
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if(col === 3){
                    if (value < 0) {
                        td.style.color = '#00f';
                    } else if (value > 0) {
                        td.style.color = '#f00';
                    }
                }
            }
        }
        return cell;
    }

    if(weeklySummaryLeftTable){
        weeklySummaryLeftTable.removeHook();
    }
    else{
        weeklySummaryLeftTable = new Handsontable(document.querySelector('#kpi-weekly-left-summary'), weeklySummaryLeftTableOptions);
    }
    weeklySummaryLeftTable.updateSettings(weeklySummaryLeftTableOptions);

    if(weeklySummaryRightTable){
        weeklySummaryRightTable.removeHook();
    }
    else{
        weeklySummaryRightTable = new Handsontable(document.querySelector('#kpi-weekly-right-summary'), weeklySummaryRightTableOptions);
    }
    weeklySummaryRightTable.updateSettings(weeklySummaryRightTableOptions);
}

function weeklyProjectReport(data) {
    const searchDate = $('#searchDate').val();
    const rows = Object.values(data);

    weeklyProjectOptions.data = rows.sort((a, b) => {
        // AD가 앞에..
        const [aName, aLocal] = a.item.split('_');
        const [bName, bLocal] = b.item.split('_');

        const aOrder = (aLocal ? 100 : 0) - projectOrder.indexOf(aName);
        const bOrder = (bLocal ? 100 : 0) - projectOrder.indexOf(bName);
        
        return aOrder - bOrder;
    });

    weeklyProjectOptions.data.push(rows.reduce((acc, row) => {
        Object.keys(row).forEach(key => {
            if (key === 'item') {
                acc.item = '합계';
            } else {
                if (!acc[key]) {
                    acc[key] = 0;
                }
                acc[key] += row[key];
            }
        });
        return acc;
    }, {}));

    weeklyProjectOptions.colHeaders = [
        '항목', '금주 Total', '지난주 Total',
    ];

    for (let i = 0; i < 7; i++) {
        weeklyProjectOptions.colHeaders.push(moment(searchDate).weekday(i).locale('kr').format('YY/MM/DD(ddd)'));
    }

    if(weeklyProjectTable){
        weeklyProjectTable.removeHook();
    }
    else{
        weeklyProjectTable = new Handsontable(document.querySelector('#kpi-weekly-project'), weeklyProjectOptions);
    }

    weeklyProjectTable.updateSettings(weeklyProjectOptions);
}

function genReferencTable(){
    referenceTableOptions.data = [
        {0 : 'D/L', 1 : '다운로드'}, 
        {0 : 'W/DAU', 1 : '주간/일일 이용자'},
        {0 : 'NRU', 1 : '신규유저'},
        {0 : 'PU', 1 : '구매유저'},
        {0 : 'PUR', 1 : '구매유저 비율'},
        {0 : 'ARPU', 1 : '이용자 평균 구매금액'},
        {0 : 'ARPPU', 1 : '구매유저 평균 구매금액'},
        {0 : 'MAU', 1 : '월 이용자'},
        {0 : 'MPU', 1 : '월 구매유저'}
    ];

    if(referenceTable){
        referenceTable.removeHook();
    }
    else{
        referenceTable = new Handsontable(document.querySelector('#kpi-reference'), referenceTableOptions);
    }
    referenceTable.updateSettings(referenceTableOptions);
}

function monthlyLargeReport(data) {
    const searchDate = $('#searchDate').val();

    monthlyReportOptions.data = Object.values(data).sort((a, b) => {
        const aOrder = monthlyItemOrder[a.item];
        const bOrder = monthlyItemOrder[b.item];

        return aOrder - bOrder;
    });

    monthlyReportOptions.colHeaders = [ '항목' ];

    for (let i = 0; i < 5; i++) {
        if (i !== 0) {
            monthlyReportOptions.colHeaders.push('전월 대비');
        }
        monthlyReportOptions.colHeaders.push(moment(searchDate).subtract(i, 'month').locale('kr').format('M월'));
    }

    const percentageRows = [];
    const revenueRows = [];
    const arpuRows = [];

    monthlyReportOptions.data.forEach((row, i) => {
        if (row.item === 'PUR') {
            percentageRows.push(i);
        }
        else if(row.item.includes('ARPU/D')) {
            arpuRows.push(i);
        }

        if (['인앱매출 (₩)', '광고매출 (₩)', '종합매출 (₩)'].includes(row.item)) {
            revenueRows.push(i);
        }
    });
    percentageRows.push(monthlyReportOptions.data.length - 1);

    monthlyReportOptions.cells = (row, col) => {
        const cell = {
            type: (col === 0) ? 'text' : 'numeric',
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                if (col === 0) {
                    Handsontable.renderers.TextRenderer.apply(this, arguments);
                    td.style.color = '#000';
                    td.style.background = '#f0f0f0';
                } else {
                    Handsontable.renderers.NumericRenderer.apply(this, arguments);
                    if (col % 2 === 0) {
                        if (value < 0) {
                            td.style.color = '#00f';
                        } else if (value > 0) {
                            td.style.color = '#f00';
                        }
                    }
                }
            }
        };
        if (col % 2 !== 0) {
            if (percentageRows.includes(row)) {
                cell.numericFormat = {
                    pattern: {
                        output: 'percent',
                        mantissa: 2,
                        trimMantissa: true,
                        spaceSeparated: true,
                    }
                };
            } else if (revenueRows.includes(row)) {
                cell.numericFormat = {
                    pattern: {
                        mantissa: 0,
                        spaceSeparated: true,
                        thousandSeparated: true
                    },
                    culture: 'ko-KR',
                };
            }
            else if(arpuRows.includes(row)){
                cell.numericFormat = {
                    pattern: '00.00'
                };
            }
        }
        return cell;
    };

    if(monthlyReportTable){
        monthlyReportTable.removeHook();
    }
    else{
        monthlyReportTable = new Handsontable(document.querySelector('#kpi-total-monthly-report'), monthlyReportOptions);
    }
    monthlyReportTable.updateSettings(monthlyReportOptions);
}

function monthlyShortReport(data) {
    const app = $('#selectProjectButton').attr('data-value');
    const isTotal = app === 'TOTAL';

    if(!isTotal) delete data['ARPU/D'];

    const searchDate = $('#searchDate').val();

    monthlyShortReportOptions.data = Object.values(data).sort((a, b) => {
        const aOrder = monthlyItemOrder[a.item];
        const bOrder = monthlyItemOrder[b.item];

        return aOrder - bOrder;
    });

    monthlyShortReportOptions.colHeaders = [ '항목' ];

    for (let i = 0; i < 3; i++) {
        if (i === 1) {
            monthlyShortReportOptions.colHeaders.push('전월 대비');
        }
        monthlyShortReportOptions.colHeaders.push(moment(searchDate).subtract(i, 'month').locale('kr').format('M월'));
    }

    const percentageRows = [];
    const revenueRows = [];
    monthlyShortReportOptions.data.forEach((row, i) => {
        if (row.item === 'PUR') {
            percentageRows.push(i);
        }
        if (['인앱매출 (₩)', '광고매출 (₩)', '종합매출 (₩)'].includes(row.item)) {
            revenueRows.push(i);
        }
    });
    percentageRows.push(monthlyShortReportOptions.data.length - 1);

    monthlyShortReportOptions.cells = (row, col) => {
        const cell = {
            type: (col === 0) ? 'text' : 'numeric',
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                if (col === 0) {
                    Handsontable.renderers.TextRenderer.apply(this, arguments);
                    td.style.color = '#000';
                    td.style.background = '#f0f0f0';
                } else {
                    Handsontable.renderers.NumericRenderer.apply(this, arguments);
                    if (col === 2) {
                        if (value < 0) {
                            td.style.color = '#00f';
                        } else if (value > 0) {
                            td.style.color = '#f00';
                        }
                    }
                }
            }
        };
        if (col !== 0 && col !== 2) {
            if (percentageRows.includes(row)) {
                cell.numericFormat = {
                    pattern: {
                        output: 'percent',
                        mantissa: 2,
                        trimMantissa: true,
                        spaceSeparated: true,
                    }
                };
            } else if (revenueRows.includes(row)) {
                cell.numericFormat = {
                    pattern: {
                        mantissa: 0,
                        spaceSeparated: true,
                        thousandSeparated: true
                    },
                    culture: 'ko-KR',
                };
            }
        }
        return cell;
    };

    delete data['PU'];

    if(monthlyShortReportTable){
        monthlyShortReportTable.removeHook();
    }
    else{
        monthlyShortReportTable = new Handsontable(document.querySelector('#kpi-monthly-report'), monthlyShortReportOptions);
    }
    monthlyShortReportTable.updateSettings(monthlyShortReportOptions);
}

function reportLoad() {
    const app = $('#selectProjectButton').attr('data-value');
    const isTotal = app === 'TOTAL';

    const end = $('#searchDate').val();

    if (isTotal) {
        $('#kpi-monthly-report').parent().parent().hide();
        $('#kpi-update-issue-left').parent().parent().hide();
        $('#kpi-update-issue-center').parent().parent().hide();
        $('#kpi-update-issue-right').parent().parent().hide();

        $('#kpi-weekly-summary-header').parent().show();
        $('#kpi-weekly-left-summary').parent().parent().show();
        $('#kpi-weekly-right-summary').parent().parent().show();

        $('#kpi-weekly-project').parent().parent().show();
        $('#kpi-total-monthly-report').parent().parent().show();
    } else {
        $('#kpi-monthly-report').parent().parent().show();
        $('#kpi-update-issue-left').parent().parent().show();
        $('#kpi-update-issue-center').parent().parent().show();
        $('#kpi-update-issue-right').parent().parent().show();

        $('#kpi-weekly-summary-header').parent().hide();
        $('#kpi-weekly-left-summary').parent().parent().hide();
        $('#kpi-weekly-right-summary').parent().parent().hide();

        $('#kpi-weekly-project').parent().parent().hide();
        $('#kpi-total-monthly-report').parent().parent().hide();
    }

    reportsLoad(app, end, (err, res) => {
        if (!err) {
            updateReportChart(res.reportMonth);
            updateWeeklyReport(res.reportWeekly);
            if (isTotal) {
                weeklySummaryReport(res.summaryWeekly);
                weeklyProjectReport(res.revenueWeekly);
                monthlyLargeReport(res.reportMonthly);
            } else {
                updateWeekRevenueChart(res.revenueOSWeekly);
                monthlyShortReport(res.reportMonthly);
            }
            genReferencTable();
        } else {
            console.log(err);
        }
    });
}

function reportsLoad(app, end, cb) {
    const isTotal = app === 'TOTAL';

    const begin = moment(end).subtract((isTotal ? 5 : 3), 'month').format('YYYY-MM-01');
    const pastMonth = moment(end).subtract(1, 'month');

    $.ajax({
        url: `/business/kpi/analytics/${app}/${begin}/${end}`,
        method: 'GET',
        dataType: 'json'
    }).done((data) => {
        if (data.success && data.analytics) {
            const apps = [];

            const reportMonth = {};
            let reportWeekly = {};
            const revenueWeekly = {};
            const revenueOSWeekly = {};
            const summaryWeekly = {};
            let reportMonthly = {};
            let middle_arpu_report = {};
            
            const searchDate = moment(end);
            const searchWeek = searchDate.week();
            data.analytics.sort((a, b) => {
                return new Date(a.date).getTime() - new Date(b.date).getTime();
            }).forEach(row => {
                //JP의 경우 Analytics에 엔화 기준으로 입력되어 있기 때문에 한화로 (강제로)변경해줄 필요가 있음
                //JP의 AD값은 KRW로 환전이 이미 된 상태이다.
                //TODO : 근본적으로 수정
                if(row.app.includes('JP') && row.JPY !== undefined) {
                    row.revenue_Android *= row.JPY;
                    row.revenue_iOS *= row.JPY;
                }

                // 날짜 계산용
                const date = moment(row.date);
                const dateStr = date.format('YYYY-MM-DD');
                const week = date.week();
                const weekday = date.weekday();
                const month = (searchDate.year() * 12 + searchDate.month()) - (date.year() * 12 + date.month());

                // 데이터 가공용
                const { app, revenue_Android, revenue_iOS, AD, DAU } = row;
                const revenue = revenue_Android + revenue_iOS;

                const revenueAll = revenue + AD;

                // 앱 이름
                if (!apps.includes(app)) {
                    apps.push(app);
                }

                // 지난 1 달간 데이터
                if (date.diff(pastMonth, 'day') >= 0) {
                    if (!reportMonth[dateStr]) {
                        reportMonth[dateStr] = {
                            revenue: 0,
                            DAU: 0
                        };
                    }
                    reportMonth[dateStr].revenue += revenueAll;
                    reportMonth[dateStr].DAU += DAU;
                }

                // 주간 데이터
                if (searchWeek === week) {
                    // 주간 지표 현황
                    Object.keys(row).forEach(key => {
                        const item = itemMap[key];
                        if (!item) return;
                        if (!reportWeekly[item]) {
                            reportWeekly[item] = {
                                item, curWeek: 0, prevWeek: 0
                            };
                        }
                        if (!reportWeekly[item][weekday]) {
                            reportWeekly[item][weekday] = 0;
                        }
                        reportWeekly[item][weekday] += row[key];
                        reportWeekly[item].curWeek += row[key];
                    });

                    if (isTotal) {
                        // 프로젝트별 매출
                        if (!revenueWeekly[app]) {
                            revenueWeekly[app] = {
                                item: app, curWeek: 0, prevWeek: 0
                            };
                        }
                        if (!revenueWeekly[app][weekday]) {
                            revenueWeekly[app][weekday] = 0;
                        }
                        revenueWeekly[app][weekday] += revenueAll;
                        revenueWeekly[app].curWeek += revenueAll;

                        // 주간 요약
                        if (!summaryWeekly[app]) {
                            summaryWeekly[app] = {
                                revenue: 0,
                                DAU: 0,
                            };
                        }
                        summaryWeekly[app].revenue += revenueAll;

                        summaryWeekly[app].DAU += DAU;
                        if(!summaryWeekly[app].ARPU) summaryWeekly[app].ARPU = 0;
                        summaryWeekly[app].ARPU += (revenueAll / DAU);
                        if(!summaryWeekly[app].dateCount) summaryWeekly[app].dateCount = 1;
                        else summaryWeekly[app].dateCount++;
                    }
                } else if (searchWeek === week + 1)  {
                    // 주간 지표 현황
                    Object.keys(row).forEach(key => {
                        const item = itemMap[key];
                        if (!item) return;
                        if (!reportWeekly[item]) {
                            reportWeekly[item] = {
                                item, curWeek: 0, prevWeek: 0
                            };
                        }
                        reportWeekly[item].prevWeek += row[key];
                    });

                    if (isTotal) {
                        // 프로젝트별 매출
                        if (!revenueWeekly[app]) {
                            revenueWeekly[app] = {
                                item: app, curWeek: 0, prevWeek: 0
                            };
                        }
                        revenueWeekly[app].prevWeek += revenueAll;
                    }
                }

                if (!isTotal) {
                    const diff = searchDate.diff(date, 'day');
                    if (diff < 7) {
                        if (!revenueOSWeekly[diff]) {
                            revenueOSWeekly[diff] = {
                                android: 0,
                                ios: 0
                            };
                        }
                        revenueOSWeekly[diff].android += revenue_Android;
                        revenueOSWeekly[diff].ios += revenue_iOS;
                    }
                }

                // 월간 지표
                Object.keys(row).forEach(key => {
                    const item = itemMap[key];
                    if (!item || (item === 'W/DAU')) return;
                    if (!reportMonthly[item]) {
                        reportMonthly[item] = { item };
                    }
                    if (!reportMonthly[item][month]) {
                        reportMonthly[item][month] = 0;
                    }
                    reportMonthly[item][month] += row[key];
                });

                if(isTotal){
                    if(!middle_arpu_report[month]) middle_arpu_report[month] = {};
                    if(!middle_arpu_report[month][dateStr]) middle_arpu_report[month][dateStr] = {
                        revenueAll : 0,
                        DAU : 0,
                        month : month
                    };
                    middle_arpu_report[month][dateStr]['revenueAll'] += revenueAll;
                    middle_arpu_report[month][dateStr]['DAU'] += DAU;
                }
            });

            if(isTotal){
                Object.values(middle_arpu_report).forEach(monthlyData => {
                    let month = Object.values(monthlyData)[0].month;
                    Object.keys(monthlyData).map(function(key, index){
                        monthlyData[key] = monthlyData[key]['revenueAll'] / monthlyData[key]['DAU'];
                    });
    
                    const arr = Object.values(monthlyData);
                    const sum = arr.reduce((a, b) => a + b, 0);
                    const avg = (sum / arr.length) || 0;
                    
                    if(!reportMonthly['ARPU/D']) reportMonthly['ARPU/D'] = { item : 'ARPU/D (₩)'};
                    reportMonthly['ARPU/D'][month] = avg;
                });

                delete middle_arpu_report;
            }

            // 주간 지표 현황 이슈
            apps.forEach(app => {
                if(isTotal) {
                    if(!app.includes('JP')){
                        reportWeekly[app] = { item: `${app} 이슈` };
                    }
                }
                else{
                    reportWeekly[app] = { item: `${app} 이슈` };
                }
            });

            let dateCount = 0;
            let totalARPU = 0;

            if(reportWeekly['인앱매출 (₩)']){
                Object.keys(reportWeekly['인앱매출 (₩)']).forEach(key => {
                    if (key === 'item') return;
                    if (!reportWeekly['종합매출 (₩)'])
                        reportWeekly['종합매출 (₩)'] = { item: '종합매출 (₩)' };
                    if (!reportWeekly.PUR)
                        reportWeekly.PUR = { item: 'PUR' };
                    if (!reportWeekly.ARPU)
                        reportWeekly.ARPU = { item: 'ARPU (₩)' };
                    if (!reportWeekly.ARPPU)
                        reportWeekly.ARPPU = { item: 'ARPPU (₩)' };
                    reportWeekly['종합매출 (₩)'][key] = reportWeekly['인앱매출 (₩)'][key] + reportWeekly['광고매출 (₩)'][key];
                    reportWeekly.PUR[key] = reportWeekly.PU[key] / reportWeekly['W/DAU'][key];
                    reportWeekly.ARPU[key] = (reportWeekly['종합매출 (₩)'][key] / reportWeekly['W/DAU'][key]);
                    reportWeekly.ARPPU[key] = reportWeekly['인앱매출 (₩)'][key] / reportWeekly.PU[key];
                
                    if((key !== '인앱매출 (₩)') && (key !== 'curWeek') && (key !== 'prevWeek')) {
                        totalARPU += reportWeekly.ARPU[key];
                        dateCount++;
                    }
                });

                // 주간 지표 현황 매출

                if (isTotal) {
                    // 주간 요약
                    summaryWeekly.revenue = reportWeekly['종합매출 (₩)'].curWeek;
                    summaryWeekly.revenueOverThePrevWeek = summaryWeekly.revenue / reportWeekly['종합매출 (₩)'].prevWeek - 1;
                    summaryWeekly.WAU = reportWeekly['W/DAU'].curWeek;
                    summaryWeekly.WAUOverThePrevWeek = summaryWeekly.WAU / reportWeekly['W/DAU'].prevWeek - 1;
                    summaryWeekly.ARPU = (totalARPU / dateCount).toFixed(2);
                }
            }

            // 월간 지표 추가 계산
            Object.keys(reportMonthly.PU).forEach(month => {
                if (month === 'item') return;
                if (!reportMonthly['종합매출 (₩)']) {
                    reportMonthly['종합매출 (₩)'] = { item: '종합매출 (₩)' };
                }
                if (!reportMonthly['광고비율']) {
                    reportMonthly['광고비율'] = { item: '광고비율' };
                }
                reportMonthly['종합매출 (₩)'][month] = reportMonthly['인앱매출 (₩)'][month] + reportMonthly['광고매출 (₩)'][month];
                reportMonthly['광고비율'][month] = reportMonthly['광고매출 (₩)'][month] / reportMonthly['종합매출 (₩)'][month];
                
                if (!reportMonthly.PUR) reportMonthly.PUR = { item: 'PUR' };
                if (!reportMonthly.ARPU) reportMonthly.ARPU = { item: 'ARPU/M (₩)' };
                if (!reportMonthly.ARPPU) reportMonthly.ARPPU = { item: 'ARPPU (₩)' };
                if (!reportMonthly.MAU) reportMonthly.MAU = { item: 'MAU' };
                if (!reportMonthly.MPU) reportMonthly.MPU = { item: 'MPU' };

                reportMonthly.PUR[month] = null;
                reportMonthly.ARPU[month] = null;
                reportMonthly.ARPPU[month] = null;
                reportMonthly.MAU[month] = null;
                reportMonthly.MPU[month] = null;
            });

            //프로젝트 단위 MAU, MPU 총합을 보여준다.
            //ARPU, ARPPU, PUR 계산하여 추가한다.
            //TODO : cb을 reportMonthly는 재조정한 이후에 보내는 방식으로 수정. Table을 조작하는게 아닌
            //reportMonthly 자체를 조작하는 방향으로...
            let promises = [];
            promises.push(new Promise((resolve, reject) => {
                changeToManualMonthlyReport(reportMonthly, searchDate, app, (err) => {
                    if(!err) resolve();
                    else reject(err);
                });
            }));
            promises.push(new Promise((resolve, reject) => {
                changeToManualWeeklyReport(reportWeekly, summaryWeekly, searchDate, app, (err) => {
                    if(!err) {
                        if(app === 'TOTAL'){
                            summaryWeekly['WAU'] = reportWeekly['W/DAU'].curWeek;

                            if(reportWeekly['W/DAU'].curWeek >= reportWeekly['W/DAU'].prevWeek){
                                summaryWeekly['WAUOverThePrevWeek'] = reportWeekly['W/DAU'].curWeek / reportWeekly['W/DAU'].prevWeek;
                            }
                            else {
                                if(reportWeekly['W/DAU'].curWeek !== null){
                                    summaryWeekly['WAUOverThePrevWeek'] = -(1 - reportWeekly['W/DAU'].curWeek / reportWeekly['W/DAU'].prevWeek);
                                }
                                else {
                                    summaryWeekly['WAUOverThePrevWeek'] = 0;
                                }
                            }
                            summaryWeekly['revenueOverThePrevWeek'] = reportWeekly['종합매출 (₩)'].curWeek / reportWeekly['종합매출 (₩)'].prevWeek - 1;
                        }
                        resolve();
                    }
                    else reject(err);
                });
            }));
            Promise.all(promises).then(x => {
                cb(null, {
                    reportMonth,
                    reportWeekly,
                    summaryWeekly,
                    revenueWeekly,
                    reportMonthly,
                    revenueOSWeekly,
                    reportMonthly
                });
            }).catch(x => {
                console.log(data);
                cb(new Error(x));
            });
        } else {
            console.log(data);
            cb(new Error('Unknown data type'));
        }
    }).fail((xhr, status, errorThrown) => {
        cb(new Error(errorThrown));
    });
}

const manualList = ['ARPPU', 'ARPU', 'PUR', 'W/DAU'];
const monthlyManualList = ['ARPPU', 'ARPU', 'PUR', 'MAU', 'MPU'];

function changeToManualWeeklyReport(reportWeekly, summaryWeekly, searchDate, app, cb){
    Object.keys(reportWeekly).forEach(key => {
        if(manualList.includes(key)) {
            reportWeekly[key].curWeek = null;
            reportWeekly[key].prevWeek = null;
        }
    });

    const data = {
        searchDate : searchDate,
        app : app,
        reportWeekly : reportWeekly,
        summaryWeekly : summaryWeekly
    }
    weeklyReportLoad(data, cb);
}

function changeToManualMonthlyReport(reportMonthly, searchDate, app, cb){
    const copied = new moment({...searchDate});
    const year = copied.year();

    const data = {
        searchDate : copied,
        year : year,
        app : app,
        reportMonthly : reportMonthly
    }
    monthlyReportLoad(data, cb);
}

function monthlyReportLoad(data, cb){
    const { searchDate, app, reportMonthly } = data;
    //Total은 기준달부터 지난 5달 MAU, MPU 정보 가져오기
    //Project 단위는 기준달부터 지난 3달 MAU, MPU 정보 가져오기
    let targetList = [];
    let num = 0;
    num = (app === 'TOTAL') ? 5 : 3;
    for(let i=0; i<num; i++){
        const copied = new moment({...searchDate});
        const targetDate = copied.subtract(i, 'M');
        const target_year = targetDate.format('YYYY');
        const target_month = targetDate.format('M');

        targetList.push({
            year : target_year,
            month : target_month
        });
    }

    const monthly_elem = $('#manual-input #manual-input-monthly');
    monthly_elem.empty();

    if(app != 'TOTAL' && app != 'SS' && app != 'SS_JP') {
        $('#manual-input').show();
        for(let i=0; i<3; i++){
            let curMonth = searchDate.clone();
            let month = curMonth.subtract(i, 'months').format('M');
            let year = curMonth.format('YYYY');
            monthly_elem.append(`
                <div class="col-md-4">
                    <label>${month}월</label><br>
                    <label style="font-size:10px;">MAU</label>
                    <input type="number" class="form-control" placeholder="MAU 입력" aria-describedby="basic-addon1" data-type="MAU" data-month=${month} data-year=${year} data-app =${app}>
                    <label style="font-size:10px;">MPU</label>
                    <input type="number" class="form-control" placeholder="MPU 입력" aria-describedby="basic-addon1" data-type="MPU" data-month=${month} data-year=${year} data-app =${app}>
                </div>
            `);
        }
    }
    else {
        $('#manual-input').hide();
    }

    $.ajax({
        url: `/business/kpi/monthlyAnalytics/${app}`,
        method: 'POST',
        dataType: 'json',
        data : {
            targetList: targetList
        }
    }).done((data) => {
        if(!data) return;

        const monthlyAnalytics = data.analytics;
        const targets = data.targetList;
        let target_index = 0;
        let val_index_arr = (app !== 'TOTAL') ? [0, 2, 3] : [0, 2, 4, 6, 8];
            for(let i=0; i<val_index_arr.length; i++){
                if(target_index >= targets.length) break;

                const findData = Object.values(monthlyAnalytics).find(x => x.month == targets[target_index].month && x.year == targets[target_index].year);
                if(findData){
                    reportMonthly['MAU'][i] = findData.mau;
                    reportMonthly['MPU'][i] = findData.mpu;
                    reportMonthly['PUR'][i] = reportMonthly['MPU'][i] / findData.mau;
                    reportMonthly['ARPU'][i] = reportMonthly['종합매출 (₩)'][i] / findData.mau;
                    reportMonthly['ARPPU'][i] = reportMonthly['인앱매출 (₩)'][i] / reportMonthly['PU'][i];
                }
                target_index++;
            }
        if(app === 'TOTAL'){ }
        else{
            Object.values(monthlyAnalytics).forEach(data => {
                if(app != data.app) return;
                let target_elem = $(monthly_elem).find(`input[data-year = '${data.year}'][data-month = '${data.month}'][data-type = 'MAU']`);
                if(target_elem) {
                    $(target_elem).val(data.mau);
                }

                target_elem = $(monthly_elem).find(`input[data-year = '${data.year}'][data-month = '${data.month}'][data-type = 'MPU']`);
                if(target_elem) {
                    $(target_elem).val(data.mpu);
                }
            });
        }
        delete reportMonthly['PU'];
        cb();
    }).fail(x => {
        cb(new Error('Response Error'));
    });
}

function weeklyReportLoad(data, cb){
    const { app, searchDate, reportWeekly, summaryWeekly } = data;
    $('#manual-input-searchDate').text('■ 기준일 ' + searchDate.format('YYYY/MM/DD'));

    //이번주, 지난주 WAU 가져오기
    $.ajax({
        url: `/business/kpi/weeklyAnalytics/${app}/${searchDate}`,
        method: 'GET',
        dataType: 'json'
    }).done((result) => {
        //금주 및 지난주 PUR, ARPU, ARPPU 계산
        if(!result) return;
        const { additional_weeklySummary, curWeek, prevWeek, curYear, prevYear } = result;
        let res_curWeek, res_prevWeek, wau_data;
        if(app === 'TOTAL' || app === 'SS' || app === 'SS_JP'){
            wau_data = result.additional_analytics;

            res_curWeek = wau_data.find(x => x.year === curYear && x.week === curWeek);
            res_prevWeek = wau_data.find(x => x.year === prevYear && x.week === prevWeek);
        }
        else{
            wau_data = result.additional_analytics;

            res_curWeek = wau_data.find(x => x.year === curYear && x.week === curWeek && x.app === app);
            res_prevWeek = wau_data.find(x => x.year === prevYear && x.week === prevWeek && x.app === app);

            $($('#manual-input #manual-input-weekly')
                .find('input')[0])
                .data('week', curWeek)
                .data('year', curYear)
                .data('app', app)
                .val(res_curWeek ? res_curWeek.wau : '');
            $($('#manual-input #manual-input-weekly')
                .find('input')[1])
                .data('week', prevWeek)
                .data('year', prevYear)
                .data('app', app)
                .val(res_prevWeek ? res_prevWeek.wau : '');
        }
        if(res_curWeek){
            reportWeekly['W/DAU']['curWeek'] = res_curWeek.wau;
            reportWeekly['PUR']['curWeek'] = reportWeekly['PU']['curWeek'] / res_curWeek.wau;
            reportWeekly['ARPU']['curWeek'] = reportWeekly['종합매출 (₩)']['curWeek'] / res_curWeek.wau;
            reportWeekly['ARPPU']['curWeek'] = reportWeekly['인앱매출 (₩)']['curWeek'] / reportWeekly['PU']['curWeek'];

            $('#manual-input #manual-input-weekly').data('arpu', reportWeekly['ARPU']['curWeek']);
            $('#manual-input #manual-input-weekly').data('arppu', reportWeekly['ARPPU']['curWeek']);
        }
        if(res_prevWeek){
            reportWeekly['W/DAU']['prevWeek'] = res_prevWeek.wau;
            reportWeekly['PUR']['prevWeek'] = reportWeekly['PU']['prevWeek'] / res_prevWeek.wau;
            reportWeekly['ARPU']['prevWeek'] = reportWeekly['종합매출 (₩)']['prevWeek'] / res_prevWeek.wau;
            reportWeekly['ARPPU']['prevWeek'] = reportWeekly['인앱매출 (₩)']['prevWeek'] / reportWeekly['PU']['prevWeek'];
        }
        cb();
    }).fail(x => {
        cb(new Error('Response Error'));
    });
}

function undateWeeklyReport(wauDatas, cb){
    $.ajax({
        url: '/business/kpi/weeklyAnalytics',
        method: 'post',
        dataType: 'json',
        data: {
            wauDatas
        }
    }).done((data) => {
        cb(data);
    });
}

function updateMonthlyReport(datas, cb){
    $.ajax({
        url: '/business/kpi/setMonthlyAnalytics',
        method: 'post',
        dataType: 'json',
        data: {
            datas
        }
    }).done((data) => {
        reportLoad();
        cb(data);
    });
}