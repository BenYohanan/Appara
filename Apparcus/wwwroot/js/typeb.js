// Type-B projects: records of payments that already happened, typed up to
// replace a paper sheet. Kept in its own file because none of it applies to
// the Type-A project screens.

// --- custom fields ---------------------------------------------------------
// Same row shape as the Type-A modals, but scoped to a container so the two
// sets cannot read each other's inputs.
function typeBFieldRow(containerId, field) {
    field = field || {};

    var html = `
        <div class="row g-2 mb-2 tb-field-row" data-id="${field.id || 0}">
            <div class="col-md-5">
                <input type="text" class="form-control tb-field-name"
                       placeholder="Field name (e.g. Reg. No.)"
                       value="${$('<div>').text(field.fieldName || '').html()}">
            </div>
            <div class="col-md-4">
                <select class="form-control tb-field-type">
                    <option value="text" ${field.fieldType === 'text' ? 'selected' : ''}>Text</option>
                    <option value="number" ${field.fieldType === 'number' ? 'selected' : ''}>Number</option>
                    <option value="date" ${field.fieldType === 'date' ? 'selected' : ''}>Date</option>
                </select>
            </div>
            <div class="col-md-2">
                <div class="form-check mt-2">
                    <input class="form-check-input tb-field-required" type="checkbox" ${field.isRequired ? 'checked' : ''}>
                    <label class="form-check-label">Required</label>
                </div>
            </div>
            <div class="col-md-1">
                <button type="button" class="btn btn-sm btn-danger"
                        onclick="$(this).closest('.tb-field-row').remove()">×</button>
            </div>
        </div>`;

    $('#' + containerId).append(html);
}

function addTypeBCustomField(field) { typeBFieldRow('tbCustomFieldsContainer', field); }
function addTypeBEditCustomField(field) { typeBFieldRow('tbeditCustomFieldsContainer', field); }

function readTypeBFields(containerId) {
    var fields = [];

    $('#' + containerId).find('.tb-field-row').each(function () {
        var name = $(this).find('.tb-field-name').val();
        if (!name) return;

        fields.push({
            Id: $(this).data('id') || 0,
            FieldName: name,
            FieldType: $(this).find('.tb-field-type').val(),
            IsRequired: $(this).find('.tb-field-required').is(':checked')
        });
    });

    return fields;
}

// --- related image ---------------------------------------------------------
// Uploaded as soon as it is chosen, so the project payload carries a path
// rather than the file itself and the create call stays a plain JSON post.
function wireImageUpload(prefix) {
    $('#' + prefix + 'image').on('change', function () {
        var file = this.files && this.files[0];
        if (!file) return;

        var data = new FormData();
        data.append('file', file);

        $.ajax({
            type: 'POST',
            url: '/api/project-image',
            data: data,
            processData: false,
            contentType: false,
            success: function (result) {
                if (result.isError) {
                    errorAlert(result.msg);
                    return;
                }
                $('#' + prefix + 'imageUrl').val(result.data.url);
                $('#' + prefix + 'imagePreview').attr('src', result.data.url).removeClass('d-none');
            },
            error: function () {
                errorAlert('That image could not be uploaded. Please try again.');
            }
        });
    });
}

// --- create / edit ---------------------------------------------------------
function saveTypeBProject() {
    var $btn = $('#tb_submit_btn');
    var original = $btn.text();
    $btn.prop('disabled', true).text('Please wait...');

    function restore() { $btn.prop('disabled', false).text(original); }

    var data = {
        Name: $('#tb_name').val(),
        Description: $('#tb_description').val(),
        CustomDate: $('#tb_customDate').val() || null,
        RelatedImageUrl: $('#tb_imageUrl').val() || null,
        IsPublic: $('#tb_isPublic').is(':checked'),
        PaymentListField: readPaymentListField('tb_'),
        CustomFields: readTypeBFields('tbCustomFieldsContainer')
    };

    if (!data.Name) {
        errorAlert('Give the project a title.');
        restore();
        return;
    }

    var codeIntent = data.IsPublic ? readAccessCodeIntent('tb_') : null;
    if (codeIntent && codeIntent.error) {
        errorAlert(codeIntent.error);
        restore();
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Project/CreateTypeB',
        dataType: 'json',
        data: { projectDetails: JSON.stringify(data) },
        success: function (result) {
            if (result.isError) {
                errorAlert(result.msg);
                restore();
                return;
            }

            var url = window.location.href;
            var newId = result.data && result.data.projectId;

            if (codeIntent && codeIntent.code && newId) {
                saveAccessCode(newId, codeIntent.code, function (err) {
                    if (err) {
                        errorAlert(err);
                        restore();
                        return;
                    }
                    successAlertWithRedirect(result.msg, url);
                });
                return;
            }

            successAlertWithRedirect(result.msg, url);
        },
        error: function () {
            errorAlert('An error has occurred, try again.');
            restore();
        }
    });
}

function getTypeBProjectById(id) {
    $.ajax({
        type: 'GET',
        url: '/Project/GetProjectById?id=' + id,
        dataType: 'json',
        success: function (project) {
            if (!project) return;

            $('#tbedit_projectId').val(project.id);
            $('#tbedit_name').val(project.name);
            $('#tbedit_description').val(project.description);
            $('#tbedit_customDate').val(project.customDate ? project.customDate.substring(0, 10) : '');
            $('#tbedit_isPublic').prop('checked', project.isPublic);

            $('#tbedit_imageUrl').val(project.relatedImageUrl || '');
            $('#tbedit_imagePreview')
                .attr('src', project.relatedImageUrl || '')
                .toggleClass('d-none', !project.relatedImageUrl);

            $('#tbeditCustomFieldsContainer').empty();
            (project.customFields || []).forEach(function (f) { addTypeBEditCustomField(f); });
            setPaymentListField('tbedit_', project.paymentListField);

            // Fresh code controls for each project opened — a code typed for
            // one project must not survive into the next.
            $('#tbedit_accessCode').val('');
            $('#tbedit_accessCodeAction').val('');
            $('#tbedit_accessCodeEditor').addClass('d-none');
            $('#tbedit_setCodeBtn').text(project.hasAccessCode ? 'Change Code' : 'Set Code');
            $('#tbedit_removeCodeBtn').toggleClass('d-none', !project.hasAccessCode);
            $('#tbedit_accessCodeState').text(project.hasAccessCode
                ? 'A 4-digit code is set. Contributors must enter it to open this project.'
                : 'No code set. Anyone who finds this project can open it.');
            syncAccessCodeVisibility('tbedit_');
        },
        error: function () {
            errorAlert('That project could not be loaded.');
        }
    });
}

function editTypeBProject() {
    var $btn = $('#tbedit_submit_btn');
    var original = $btn.text();
    $btn.prop('disabled', true).text('Please wait...');

    function restore() { $btn.prop('disabled', false).text(original); }

    var data = {
        Id: parseInt($('#tbedit_projectId').val()),
        Name: $('#tbedit_name').val(),
        Description: $('#tbedit_description').val(),
        CustomDate: $('#tbedit_customDate').val() || null,
        RelatedImageUrl: $('#tbedit_imageUrl').val() || null,
        IsPublic: $('#tbedit_isPublic').is(':checked'),
        PaymentListField: readPaymentListField('tbedit_'),
        CustomFields: readTypeBFields('tbeditCustomFieldsContainer')
    };

    if (!data.Name) {
        errorAlert('Give the project a title.');
        restore();
        return;
    }

    var codeIntent = readAccessCodeIntent('tbedit_');
    if (codeIntent && codeIntent.error) {
        errorAlert(codeIntent.error);
        restore();
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Project/Edit',
        dataType: 'json',
        data: { projectDetails: JSON.stringify(data) },
        success: function (result) {
            if (result.isError) {
                errorAlert(result.msg);
                restore();
                return;
            }

            var url = window.location.href;

            if (codeIntent) {
                saveAccessCode(data.Id, codeIntent.code, function (err) {
                    if (err) {
                        errorAlert(err);
                        restore();
                        return;
                    }
                    successAlertWithRedirect(result.msg, url);
                });
                return;
            }

            successAlertWithRedirect(result.msg, url);
        },
        error: function () {
            errorAlert('An error occurred. Please try again.');
            restore();
        }
    });
}

// --- records ---------------------------------------------------------------
function openAddRecord(projectId, projectName) {
    $('#record_projectId').val(projectId);
    $('#record_projectName').text(projectName || '');
    $('#record_error, #record_saved').addClass('d-none');
    clearRecordInputs();

    // The fields to ask for are the ones this project was set up with, so
    // they are read off the project rather than assumed.
    $.ajax({
        type: 'GET',
        url: '/Project/GetProjectById?id=' + projectId,
        dataType: 'json',
        success: function (project) {
            var container = $('#recordCustomFields').empty();

            (project && project.customFields || []).forEach(function (f) {
                var label = $('<div>').text(f.fieldName || '').html();
                var type = f.fieldType === 'number' ? 'number' : (f.fieldType === 'date' ? 'date' : 'text');

                container.append(`
                    <div class="col-md-6">
                        <label class="form-label">${label}${f.isRequired ? ' <span class="text-danger">*</span>' : ''}</label>
                        <input type="${type}" class="form-control record-field"
                               data-field-id="${f.id}" data-required="${f.isRequired ? 1 : 0}">
                    </div>`);
            });
        }
    });
}

function clearRecordInputs() {
    $('#record_name, #record_amount, #record_phone').val('');
    $('.record-field').val('');
}

function saveRecord(keepOpen) {
    var $btn = keepOpen ? $('#record_next_btn') : $('#record_done_btn');
    var original = $btn.text();
    $btn.prop('disabled', true).text('Saving...');

    function restore() { $btn.prop('disabled', false).text(original); }

    function showError(message) {
        $('#record_error').text(message).removeClass('d-none');
        $('#record_saved').addClass('d-none');
    }

    var data = {
        ProjectId: parseInt($('#record_projectId').val()),
        FullName: $('#record_name').val(),
        Amount: parseFloat($('#record_amount').val()),
        PhoneNumber: $('#record_phone').val(),
        FieldValues: []
    };

    if (!data.FullName) {
        showError("Enter the contributor's name.");
        restore();
        return;
    }

    if (!data.Amount || data.Amount <= 0) {
        showError('Enter the amount paid.');
        restore();
        return;
    }

    $('.record-field').each(function () {
        data.FieldValues.push({
            ProjectCustomFieldId: $(this).data('field-id'),
            Value: $(this).val()
        });
    });

    $.ajax({
        type: 'POST',
        url: '/Project/AddRecord',
        dataType: 'json',
        data: { recordDetails: JSON.stringify(data) },
        success: function (result) {
            if (result.isError) {
                showError(result.msg);
                restore();
                return;
            }

            if (!keepOpen) {
                successAlertWithRedirect(result.msg, window.location.href);
                return;
            }

            // Next: keep the form open for the following name on the sheet,
            // and keep the running count honest while they work through it.
            var quota = result.data || {};
            $('#record_quotaLine').text(
                (quota.remaining || 0).toLocaleString() + ' of ' +
                (quota.quota || 0).toLocaleString() + ' records left');

            $('#record_error').addClass('d-none');
            $('#record_saved').text('Saved "' + data.FullName + '". Enter the next one.').removeClass('d-none');

            clearRecordInputs();
            $('#record_name').trigger('focus');
            restore();
        },
        error: function () {
            showError('That record could not be saved. Please try again.');
            restore();
        }
    });
}

// --- record allowance ------------------------------------------------------
// The button says what the chosen pack costs, so nobody pays without having
// read the price.
$(document).on('change', 'input[name="record_pack"]', function () {
    var price = $(this).closest('label').find('span').last().text().trim();
    $('#buy_records_btn').text(price ? 'Pay ' + price : 'Pay');
});

$(document).on('shown.bs.modal', '#buy_records', function () {
    $('input[name="record_pack"]:checked').trigger('change');
});

function buyRecordPack() {
    var $btn = $('#buy_records_btn');
    var original = $btn.text();

    var packSize = $('input[name="record_pack"]:checked').val();

    if (!packSize) {
        $('#buy_records_error').text('Choose a pack first.').removeClass('d-none');
        return;
    }

    $('#buy_records_error').addClass('d-none');
    $btn.prop('disabled', true).text('Please wait...');

    $.ajax({
        type: 'POST',
        url: '/api/record-quota/buy?packSize=' + encodeURIComponent(packSize),
        dataType: 'json',
        success: function (result) {
            if (result.isError) {
                $('#buy_records_error').text(result.msg).removeClass('d-none');
                $btn.prop('disabled', false).text(original);
                return;
            }
            successAlertWithRedirect(result.msg, window.location.href);
        },
        error: function () {
            $('#buy_records_error')
                .text('That purchase could not be completed. Please try again.')
                .removeClass('d-none');
            $btn.prop('disabled', false).text(original);
        }
    });
}

// --- share -----------------------------------------------------------------
function shareTypeB(projectId) {
    var url = window.location.origin + '/Guest/View/' + projectId;

    if (navigator.share) {
        navigator.share({ url: url }).catch(function () { /* dismissed */ });
        return;
    }

    if (navigator.clipboard) {
        navigator.clipboard.writeText(url).then(function () {
            successAlert('Link copied.');
        }).catch(function () {
            window.prompt('Copy this link:', url);
        });
        return;
    }

    window.prompt('Copy this link:', url);
}

$(function () {
    wireImageUpload('tb_');
    wireImageUpload('tbedit_');
});
