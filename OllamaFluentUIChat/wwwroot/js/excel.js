window.downloadFileFromBase64 = (fileName, base64String) => {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,' + base64String;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}