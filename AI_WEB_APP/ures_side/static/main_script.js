$(document).ready(function () {

    var counter = 0;

    $('#file_upload').on('change', function () {
        let file = $('#file_upload').prop('files')[0];
        let reader = new FileReader();
        reader.readAsDataURL(file);

        reader.onload = function (e) {

            let data = {
                id: counter++,
                img: reader.result
            }

            let elem = '<div class="image loading" id = "' + data.id + '"> ' +
                '<div class="element">' +
                '<img src="' + data.img + '">' +
                '<div class="classname">ClassName: <span>kit</span></div>' +
                '<div class="confidence">Confidence: <span>100%</span></div>' +
                '</div>' +
                '<div class="blur">' +
                '<img src="./static/load.png">' +
                '</div>' +
                '</div >';

            data.img = reader.result.replace('data:image/jpeg;base64,', '')

            $('.images').prepend(elem);

            $.ajax({
                url: 'http://localhost:5257/Photo',
                method: 'post',
                contentType: "application/json; charset=utf-8",
                data: JSON.stringify(data),
                success: (data) => {

                    $('#' + data[0].id).remove();
                    
                    $(data).each(function (index, item) {
                        let elem = '<div class="image" id = "' + item.id + '"> ' +
                            '<div class="element">' +
                            '<img src="data:image/jpeg;base64,' + item.img + '">' +
                            '<div class="classname">ClassName: <span>' + item.class + '</span></div>' +
                            '<div class="confidence">Confidence: <span>' + item['ñonfidence'].toFixed(2) + '</span></div>' +
                            '</div>' +
                            '</div >';

                        $('.images').prepend(elem);
                    });

                },
                error: (ex) => {
                    console.log(ex);
                }
            });

        };
        
    });

});

