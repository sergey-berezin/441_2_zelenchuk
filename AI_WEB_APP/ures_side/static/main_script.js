$(document).ready(function () {
    const toBase64 = file => new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.readAsDataURL(file);
        reader.onload = () => resolve(reader.result);
        reader.onerror = reject;
    });

    

    $('#upload_file').on('change', function () {
        console.log("ok, jivem!!!");
    });

});