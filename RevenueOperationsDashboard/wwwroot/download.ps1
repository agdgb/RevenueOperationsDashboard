$wwwroot = "c:\Users\User\source\repos\OperationDashboard Project\RevenueOperationsDashboard\wwwroot"

Invoke-WebRequest -Uri "https://cdn.tailwindcss.com" -OutFile "$wwwroot\tailwindcss.js"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js" -OutFile "$wwwroot\echarts.min.js"

New-Item -ItemType Directory -Force -Path "$wwwroot\css"
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" -OutFile "$wwwroot\css\all.min.css"

New-Item -ItemType Directory -Force -Path "$wwwroot\webfonts"
$fonts = @(
    "fa-solid-900.woff2",
    "fa-solid-900.ttf",
    "fa-regular-400.woff2",
    "fa-regular-400.ttf",
    "fa-brands-400.woff2",
    "fa-brands-400.ttf",
    "fa-v4compatibility.woff2",
    "fa-v4compatibility.ttf"
)

foreach ($font in $fonts) {
    Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/webfonts/$font" -OutFile "$wwwroot\webfonts\$font"
}
