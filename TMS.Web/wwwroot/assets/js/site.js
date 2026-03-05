// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ✅ SAFEGUARD: Wrap in jQuery ready with local $ variable
jQuery(function ($) {
    $("#loaderbody").addClass('hide');

    $(document).bind('ajaxStart', function () {
        $("#loaderbody").removeClass('hide');
    }).bind('ajaxStop', function () {
        $("#loaderbody").addClass('hide');
    });

    var trigger = $('.hamburger'),
        overlay = $('.overlay'),
        isClosed = false;

    trigger.click(function () {
        hamburger_cross();
    });

    function hamburger_cross() {
        if (isClosed == true) {
            overlay.hide();
            trigger.removeClass('is-open');
            trigger.addClass('is-closed');
            isClosed = false;
        } else {
            overlay.show();
            trigger.removeClass('is-closed');
            trigger.addClass('is-open');
            isClosed = true;
        }
    }

    $('[data-toggle="offcanvas"]').click(function () {
        $('#wrapper').toggleClass('toggled');
    });
});

showInPopup = (url, title) => {
    // ✅ SAFEGUARD: Force $ to be jQuery
    var $ = jQuery;
    $.ajax({
        type: "GET",
        url: url,
        success: function (res) {
            // ✅ SAFEGUARD: Force $ to be jQuery inside callback
            var $ = jQuery;
            console.log(res);
            $("#form-modal .modal-body").html(res);
            $("#form-modal .modal-title").html(title);
            $("#form-modal").modal('show');
        }
    })
}

showInPopupLg = (url, title) => {
    // ✅ SAFEGUARD: Force $ to be jQuery
    var $ = jQuery;
    $.ajax({
        type: "GET",
        url: url,
        success: function (res) {
            var $ = jQuery;
            $("#form-modal-lg .modal-body").html(res);
            $("#form-modal-lg .modal-title").html(title);
            $("#form-modal-lg").modal('show');
        }
    })
}

jQueryAjaxPost = form => {
    // ✅ SAFEGUARD: Force $ to be jQuery
    var $ = jQuery;
    try {
        $.ajax({
            type: 'POST',
            url: form.action,
            data: new FormData(form),
            contentType: false,
            processData: false,
            success: function (res) {
                var $ = jQuery; // ✅ SAFEGUARD
                if (res.isValid) {
                    console.log('Save successful:', res);
                    $('#view-all').html(res.html);
                    $('#form-modal').modal('hide');
                    location.reload(true);
                }
                else {
                    console.error('Save failed:', res);
                    if (res.errorMessage) {
                        console.error('Error Message:', res.errorMessage);
                        console.error('Inner Error:', res.innerError);
                        alert('Error: ' + res.errorMessage + (res.innerError ? '\n\nDetail: ' + res.innerError : ''));
                    }
                    $('#form-modal .modal-body').html(res.html);
                }
            },
            error: function (err) {
                console.log(err);
                alert('AJAX Error: ' + err.statusText);
            }
        })
        //to prevent default form submit event
        return false;
    } catch (ex) {
        console.log(ex);
        alert('Exception: ' + ex.message);
    }
}

jQueryAjaxDelete = form => {
    // ✅ SAFEGUARD: Force $ to be jQuery
    var $ = jQuery;
    if (confirm('Are you sure want to delete this record ?')) {
        try {
            $.ajax({
                type: 'POST',
                url: form.action,
                data: new FormData(form),
                contentType: false,
                processData: false,
                success: function (res) {
                    $('#view-all').html(res.html);
                    location.reload(true);
                    M.toast({ html: 'Record deleted successfully' })
                },
                error: function (err) {
                    console.log(err);
                }
            })
        } catch (ex) {
            console.log(ex);
        }
    }

    //prevent default form submit event
    return false;
}

refreshTable = (tblName) => {
    // ✅ SAFEGUARD: Force $ to be jQuery
    var $ = jQuery;
    oTable = $('#' + tblName).DataTable();
    oTable.ajax.reload(null, false);
}