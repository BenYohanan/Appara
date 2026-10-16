let contributionId = null;
let paymentTimer = null;
let paymentPoller = null;


let connection = null;

if (typeof signalR !== "undefined") {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/paymentHub")
        .withAutomaticReconnect()
        .build();

    connection.start()
        .then(function () {
            console.log("SignalR Connected");
        })
        .catch(function (err) {
            console.error(err.toString());
        });

}

function login() {
    var $submitBtn = $("#submit_btn");
    $submitBtn.prop("disabled", true).text("Please wait...");

    var email = $('#email').val().trim();
    var password = $('#password').val();

    if (!email) {
        errorAlert("Email is required.");
        $submitBtn.prop("disabled", false).text("Login");
        return;
    }

    if (!password) {
        errorAlert("Password is required.");
        $submitBtn.prop("disabled", false).text("Login");
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Account/Login',
        dataType: 'json',
        data: {
            email: email,
            password: password
        },
        success: function (result) {
            $submitBtn.prop("disabled", false).text("Login");

            if (!result.isError) {
                location.href = result.returnUrl;
            } else {
                if (result.data != null) {
                    newSuccessAlert(result.msg, result.url);
                } else {
                    errorAlert(result.msg || "Login failed. Please try again.");
                }
            }
        },
        error: function (ex) {
            $submitBtn.prop("disabled", false).text("Login");
            errorAlert("An error has occurred. Please try again or contact support if the issue persists.");
        }
    });
}

function registerUser() {
    var $submitBtn = $("#submit_btn");
    $submitBtn.prop("disabled", true).text("Please wait...");

    $("#checkboxWarning").hide();

    if (!$('#privacyCheck').is(':checked') || !$('#nameCheck').is(':checked')) {

        $("#checkboxWarning").show();
        $submitBtn.prop("disabled", false).text("Sign Up");
        return;
    }

    var email = $('#email').val().trim();
    var firstName = $('#firstName').val().trim();
    var password = $('#password').val();
    var confirmPassword = $('#confirmPassword').val();
    var phone = $('#phone').val().trim();
    var lastName = $('#lastName').val().trim();
    var address = $('#address').val().trim();
    var dateOfBirth = $('#dateOfBirth').val().trim();

    if (!email) {
        errorAlert("Email is required.");
        $submitBtn.prop("disabled", false).text("Register");
        return;
    }
    if (!firstName) {
        errorAlert("First Name is required.");
        $submitBtn.prop("disabled", false).text("Register");
        return;
    }
    if (!password) {
        errorAlert("Password is required.");
        $submitBtn.prop("disabled", false).text("Register");
        return;
    }
    if (!confirmPassword) {
        errorAlert("Please confirm your password.");
        $submitBtn.prop("disabled", false).text("Register");
        return;
    }
    if (password !== confirmPassword) {
        errorAlert("Passwords do not match.");
        $submitBtn.prop("disabled", false).text("Register");
        return;
    }
 
    var data = {
        Email: email,
        PhoneNumber: phone,
        FirstName: firstName,
        LastName: lastName,
        Address: address,
        Password: password,
        DateOfBirth: dateOfBirth,
        confirmPassword: confirmPassword
    };

    $.ajax({
        type: 'POST',
        url: '/Account/Register',
        dataType: 'json',
        data: {
            userData: JSON.stringify(data)
        },
        success: function (result) {
            if (!result.isError) {
                newSuccessAlert(result.msg);
                setTimeout(function () {
                    window.location.href = '/Account/Login';
                }, 2000); 
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Register");
            }
        },
        error: function (ex) {
            errorAlert("An error has occurred, try again. Please contact support if the error persists.");
            $submitBtn.prop("disabled", false).text("Register");
        }
    });
}

function openChangePasswordModal(userId) {
    $('#userIdForPassword').val(userId);
    $('#change_password_modal').modal('show');
}

function openMakeAdminModal(userId) {
    $('#userIdToMakeId').val(userId);
    $('#makeAdmin_modal').modal('show');
}

function changePassword() {
    var userId = $('#userIdForPassword').val();
    var newPassword = $('#newPassword').val();
    if (!newPassword) {
        infoAlert('Please enter a new password.');
        return;
    }
    var defaultBtnValue = $('#change_pass_btn').html();
    $('#change_pass_btn').html("Please wait...").attr("disabled", true);

    $.ajax({
        type: 'POST',
        url: '/Admin/ChangePassword',
        dataType: 'json',
        data: { userId: userId, newPassword: newPassword },
        success: function (result) {
            if (!result.isError) {
                newSuccessAlert(result.msg, location.href);
            } else {
                errorAlert(result.msg);
            }
            $('#change_pass_btn').html(defaultBtnValue).attr("disabled", false);
        },
        error: function () {
            errorAlert("An error has occurred, please try again.");
            $('#change_pass_btn').html(defaultBtnValue).attr("disabled", false);
        }
    });
}
function makeUserAdmin() {
    const userId = $('#userIdToMakeId').val();
    if (!userId) {
        errorAlert("Invalid user ID");
        return;
    }

    $.ajax({
        url: '/Admin/MakeUserAdmin',
        type: 'POST',
        data: { userId: userId },
        success: function (response) {
            if (!response.isError) {
                var url = location.href;
                newSuccessAlert(response.msg, url);
            } else {
                errorAlert(response.msg);
            }
        },
        error: function () {
            errorAlert("An error occurred while making user admin.");
        }
    });
}
function openRemoveAdminModal(id) {
    $('#userIdToRemoveId').val(id);
    $('#removeAdmin_modal').modal('show');
}
function removeUserAdmin() {
    const userId = $('#userIdToRemoveId').val();
    if (!userId) {
        errorAlert("Invalid user ID");
        return;
    }

    $.ajax({
        url: '/Admin/RemoveUserAdmin',
        type: 'POST',
        data: { userId: userId },
        success: function (response) {
            if (!response.isError) {
                var url = location.href;
                newSuccessAlert(response.msg, url);
            } else {
                errorAlert(response.msg);
            }
        },
        error: function () {
            errorAlert("An error occurred while removing admin role.");
        }
    });
}
function openDeleteUserModal(userId) {
    $('#userIdToDelete').val(userId);
    $('#delete_modal').modal('show');
}
function deleteUsers() {
    $.ajax({
        type: 'Post',
        dataType: 'json',
        url: '/Admin/Delete',
        data:
        {
            userId: $('#userIdToDelete').val(),
        },
        success: function (result) {
            if (!result.isError) {
                var url = window.location.href;
                newSuccessAlert(result.msg, url)
            }
            else {
                errorAlert(result.msg)
            }
        },
        error: function (ex) {
            errorAlert("An error has occurred, try again. Please contact support if the error persists");
        }
    });
}
$(function () {
    $(".select2").select2();
});

$('#withdrawModal').on('shown.bs.modal', function () {
    $('#bankCode').select2({
        dropdownParent: $('#withdrawModal'),
        width: '100%'
    });

    loadWithdrawalInfo();
});


// --- Project access code -------------------------------------------------
// The 4-digit code contributors enter before a public project page opens.
// It is never part of the project payload: the modals collect it here and
// save it through /Project/SetAccessCode once the project itself is saved.

// The code only means anything on a public project, so the whole block
// follows the "Make Project Name Public" toggle.
function syncAccessCodeVisibility(prefix) {
    var isPublic = $('#' + prefix + 'isPublic').is(':checked');
    $('#' + prefix + 'accessCodeSection').toggleClass('d-none', !isPublic);
}

function toggleAccessCodeEditor(prefix) {
    var $editor = $('#' + prefix + 'accessCodeEditor');
    var opening = $editor.hasClass('d-none');

    $editor.toggleClass('d-none', !opening);
    if (opening) {
        $('#' + prefix + 'accessCode').val('').trigger('focus');
        $('#' + prefix + 'accessCodeAction').val('');
    }
}

function clearAccessCode(prefix) {
    $('#' + prefix + 'accessCode').val('');
    $('#' + prefix + 'accessCodeAction').val('clear');
    $('#' + prefix + 'accessCodeEditor').addClass('d-none');
    $('#' + prefix + 'accessCodeState')
        .text('The code will be removed when you click Update.');
}

// Reads the modal and answers what should happen to the code, or null when
// nothing should. Returns a string when the caller must report a problem.
function readAccessCodeIntent(prefix) {
    var code = ($('#' + prefix + 'accessCode').val() || '').trim();
    var action = $('#' + prefix + 'accessCodeAction').val();

    if (action === 'clear') return { code: '' };
    if (!code) return null;

    if (!/^[0-9]{4}$/.test(code)) return { error: 'The code must be exactly 4 digits.' };

    return { code: code };
}

// Saves the code against a project that already exists. Reports failure
// rather than swallowing it: the project saved but the gate did not, and
// the owner would otherwise hand out a code that does not work.
function saveAccessCode(projectId, code, onDone) {
    $.ajax({
        type: 'POST',
        url: '/Project/SetAccessCode',
        dataType: 'json',
        data: { id: projectId, code: code },
        success: function (result) {
            onDone(result.isError ? result.msg : null);
        },
        error: function () {
            onDone('The project was saved, but the access code could not be. Please set it again from Edit.');
        }
    });
}

$(function () {
    ['', 'edit_'].forEach(function (prefix) {
        var $toggle = $('#' + prefix + 'isPublic');
        if (!$toggle.length) return;

        $toggle.on('change', function () { syncAccessCodeVisibility(prefix); });
        syncAccessCodeVisibility(prefix);

        $('#' + prefix + 'accessCode').on('input', function () {
            this.value = this.value.replace(/[^0-9]/g, '').slice(0, 4);
            $('#' + prefix + 'accessCodeAction').val(this.value ? 'set' : '');
        });
    });
});

function saveProject() {
    var $submitBtn = $("#submit_btn");
    var originalText = $submitBtn.text();
    $submitBtn.prop("disabled", true).text("Please wait...");
    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }
    var data = {};
    data.Name = $('#name').val();
    data.Description = $('#description').val();
    data.AmountNeeded = parseFloat($('#amountNeeded').val());
    data.ShowForAudit = $('#showForAudit').is(':checked');
    data.IsPublic = $('#isPublic').is(':checked');
    data.PaymentListField = readPaymentListField('');
    data.CustomFields = [];

    $('.custom-field-row').each(function () {
        var fieldName = $(this).find('.field-name').val();
        if (!fieldName) return;

        data.CustomFields.push({
            FieldName: fieldName,
            FieldType: $(this).find('.field-type').val(),
            IsRequired: $(this).find('.field-required').is(':checked')
        });
    });

    if (!data.Name) {
        errorAlert("Name is required.");
        restoreButton();
        return;
    }
    if (!data.AmountNeeded) {
        errorAlert("Amount is required.");
        restoreButton();
        return;
    }

    var codeIntent = data.IsPublic ? readAccessCodeIntent('') : null;
    if (codeIntent && codeIntent.error) {
        errorAlert(codeIntent.error);
        restoreButton();
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Project/Create',
        dataType: 'json',
        data: {
            projectDetails: JSON.stringify(data)
        },
        success: function (result) {
            if (result.isError) {
                errorAlert(result.msg);
                restoreButton();
                return;
            }

            var url = window.location.href;
            var newId = result.data && result.data.projectId;

            // The project fee, when one applied, is said in the same alert
            // that confirms the project — especially when it is still owed.
            if (result.data && result.data.feeNote) {
                result.msg = result.msg + '. ' + result.data.feeNote;
            }

            // The code is a second write against the project that was just
            // created, so it can only run once we know its id.
            if (codeIntent && codeIntent.code && newId) {
                saveAccessCode(newId, codeIntent.code, function (err) {
                    if (err) {
                        errorAlert(err);
                        restoreButton();
                        return;
                    }
                    successAlertWithRedirect(result.msg, url);
                });
                return;
            }

            successAlertWithRedirect(result.msg, url);
        },
        error: function () {
            errorAlert("An error has occurred, try again.");
            restoreButton();
        }
    });
}

function getProjectById(id) {
    $.ajax({
        type: "GET",
        url: "/Project/GetProjectById?id=" + id,
        dataType: "json",
        success: function (project) {
            $('#edit_projectId').val(project.id);
            $('#edit_name').val(project.name);
            $('#shareLinkInput').val(project.supportLink);
            $('#shareProjectTitle').text(`Name: ${project.name}`);
            $('#shareProjectAmount').text(`Target Amount: ₦${project.amountNeeded}`);
            $('#edit_description').summernote('code', project.description);
            $('#edit_amountNeeded').val(project.amountNeeded);
            $('#edit_accountNumber').val(project.accountNumber);
            $('#edit_showForAudit').prop('checked', project.showForAudit);
            $('#edit_isPublic').prop('checked', project.isPublic);

            // Fresh controls for each project opened — a code typed for one
            // project must not survive into the next.
            $('#edit_accessCode').val('');
            $('#edit_accessCodeAction').val('');
            $('#edit_accessCodeEditor').addClass('d-none');
            $('#edit_setCodeBtn').text(project.hasAccessCode ? 'Change Code' : 'Set Code');
            $('#edit_removeCodeBtn').toggleClass('d-none', !project.hasAccessCode);
            $('#edit_accessCodeState').text(project.hasAccessCode
                ? 'A 4-digit code is set. Contributors must enter it to open this project.'
                : 'No code set. Anyone who finds this project can open it.');
            syncAccessCodeVisibility('edit_');
            $('#edit_accountHolderName').val(project.accountHolderName);
            $('#editCustomFieldsContainer').empty();

            if (project.customFields && project.customFields.length > 0) {
                project.customFields.forEach(f => {
                    addEditCustomField(f);
                });
            }
            setPaymentListField('edit_', project.paymentListField);
            var shareUrl = project.supportLink;
            var shareText = 'Support my request: ' + project.name;

            $('#shareWhatsapp').attr('href',
                'https://wa.me/?text=' + encodeURIComponent(shareUrl));

            $('#shareTwitter').attr('href',
                'https://twitter.com/intent/tweet?url=' + encodeURIComponent(shareUrl) +
                '&text=' + encodeURIComponent(shareText));

            $('#shareFacebook').attr('href',
                'https://www.facebook.com/sharer/sharer.php?u=' + encodeURIComponent(shareUrl));

            $('#shareTelegram').attr('href',
                'https://t.me/share/url?url=' + encodeURIComponent(shareUrl) +
                '&text=' + encodeURIComponent(shareText));

            const qrImage = document.getElementById('qrCodeImage');
            if (project.qrCodeBase64) {
                qrImage.src = "data:image/png;base64," + project.qrCodeBase64;
                qrImage.style.display = "block";
            }
        },
        error: function () {
            errorAlert("Failed to load project.");
        }
    });
}

function addEditCustomField(field = {}) {
    var html = `
    <div class="row mb-2 custom-field-row-edit" data-id="${field.id || 0}">
        <div class="col-md-5">
            <input type="text" class="form-control field-name-edit"
                   value="${field.fieldName || ''}">
        </div>

        <div class="col-md-4">
            <select class="form-control field-type-edit">
                <option value="text" ${field.fieldType === 'text' ? 'selected' : ''}>Text</option>
                <option value="number" ${field.fieldType === 'number' ? 'selected' : ''}>Number</option>
                <option value="date" ${field.fieldType === 'date' ? 'selected' : ''}>Date</option>
            </select>
        </div>

        <div class="col-md-2">
            <div class="form-check mt-2">
                  <input type="checkbox" class="form-check-input field-required-edit" ${field.isRequired ? 'checked' : ''}>
                 <label class="form-check-label">Required</label>
            </div>
        </div>

        <div class="col-md-1">
            <button type="button" class="btn btn-danger btn-sm"
                onclick="$(this).closest('.custom-field-row-edit').remove()">×</button>
        </div>
    </div>`;

    $('#editCustomFieldsContainer').append(html);
}

function editProject() {
    var $submitBtn = $("#edit_submit_btn");
    var originalText = $submitBtn.text();
    $submitBtn.prop("disabled", true).text("Please wait...");

    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }

    var data = {};
    data.Id = parseInt($('#edit_projectId').val());
    data.Name = $('#edit_name').val();
    data.Description = $('#edit_description').val();
    data.AmountNeeded = parseFloat($('#edit_amountNeeded').val());
    data.ShowForAudit = $('#edit_showForAudit').is(':checked');
    data.IsPublic = $('#edit_isPublic').is(':checked');
    data.PaymentListField = readPaymentListField('edit_');
    data.CustomFields = [];

    $('.custom-field-row-edit').each(function () {
        var fieldName = $(this).find('.field-name-edit').val();
        if (!fieldName) return;

        data.CustomFields.push({
            Id: $(this).data("id") || 0,
            FieldName: fieldName,
            FieldType: $(this).find('.field-type-edit').val(),
            IsRequired: $(this).find('.field-required-edit').is(':checked')
        });
    });
    if (!data.Name) {
        errorAlert("Name is required.");
        restoreButton();
        return;
    }

    if (!data.AmountNeeded) {
        errorAlert("Amount Needed is required.");
        restoreButton();
        return;
    }

    var codeIntent = readAccessCodeIntent('edit_');
    if (codeIntent && codeIntent.error) {
        errorAlert(codeIntent.error);
        restoreButton();
        return;
    }

    $.ajax({
        type: "POST",
        url: "/Project/Edit",
        dataType: "json",
        data: {
            projectDetails: JSON.stringify(data)
        },
        success: function (result) {
            if (result.isError) {
                errorAlert(result.msg);
                restoreButton();
                return;
            }

            var url = window.location.href;

            if (codeIntent) {
                saveAccessCode(data.Id, codeIntent.code, function (err) {
                    if (err) {
                        errorAlert(err);
                        restoreButton();
                        return;
                    }
                    successAlertWithRedirect(result.msg, url);
                });
                return;
            }

            successAlertWithRedirect(result.msg, url);
        },
        error: function () {
            errorAlert("An error occurred. Please try again.");
            restoreButton();
        }
    });
}

function confirmDeleteProject(id) {
    $('#delete_projectId, #close_projectId, #activate_projectId').val(id);
}
function confirmApproveDeletePayment(contributorId, projectId) {
    $('#projectId').val(projectId);
    $('#supporterId').val(contributorId);
}

function deleteProject() {
    var $submitBtn = $("#delete_submit_btn");
    var originalText = $submitBtn.text();
    $submitBtn.prop("disabled", true).text("Deleting...");

    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }

    var projectId = parseInt($('#delete_projectId').val());

    if (!projectId) {
        errorAlert("Invalid project ID.");
        restoreButton();
        return;
    }

    $.ajax({
        type: "POST",
        url: "/Project/Delete",
        dataType: "json",
        data: { id: projectId },
        success: function (result) {
            if (!result.isError) {
                var url = window.location.href;
                $('#delete_project_modal').modal('hide');
                successAlertWithRedirect(result.msg, url);
            } else {
                errorAlert(result.msg);
                restoreButton();
            }
        },
        error: function () {
            errorAlert("An error occurred. Please try again.");
            restoreButton();
        }
    });
}

function updateProject(status, inputId, buttonId, modalId) {
    var $submitBtn = $("#" + buttonId);
    var originalText = $submitBtn.text();

    $submitBtn.prop("disabled", true).text("Please wait...");

    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }

    var projectId = parseInt($("#" + inputId).val());

    if (!projectId) {
        errorAlert("Invalid project ID.");
        restoreButton();
        return;
    }

    $.ajax({
        type: "POST",
        url: "/Project/UpdateStatus",
        dataType: "json",
        data: { id: projectId, status: status },
        success: function (result) {
            if (!result.isError) {
                $("#" + modalId).modal("hide");
                successAlertWithRedirect(result.msg, window.location.href);
            } else {
                errorAlert(result.msg);
                restoreButton();
            }
        },
        error: function () {
            errorAlert("An error occurred. Please try again.");
            restoreButton();
        }
    });
}
function addUser() {
    var $submitBtn = $("#add_user_btn");
    var originalText = $submitBtn.text();
    $submitBtn.prop("disabled", true).text("Please wait...");

    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }

    var data = {};
    data.FirstName = $("#firstName").val().trim();
    data.LastName = $("#lastName").val().trim();
    data.Email = $("#email").val().trim();
    data.PhoneNumber = $("#phoneNumber").val().trim();
    data.DateOfBirth = $("#dob").val();
    data.Password = $("#password").val();
    data.ConfirmPassword = $("#confirmPassword").val();
    data.IsAdmin = $("#isAdmin").val() === "true";

    if (!data.FirstName) {
        errorAlert("First Name is required.");
        restoreButton();
        return;
    }
    if (!data.LastName) {
        errorAlert("Last Name is required.");
        restoreButton();
        return;
    }
    if (!data.Email) {
        errorAlert("Email is required.");
        restoreButton();
        return;
    }
    if (!data.Password || !data.ConfirmPassword) {
        errorAlert("Password and confirmation are required.");
        restoreButton();
        return;
    }
    if (data.Password !== data.ConfirmPassword) {
        errorAlert("Passwords do not match.");
        restoreButton();
        return;
    }

    $.ajax({
        type: "POST",
        url: "/User/CreateFromAdmin",
        dataType: "json",
        data: { userDetails: JSON.stringify(data) },
        success: function (result) {
            if (result.success) {
                successAlertWithRedirect(result.message, "/User/Index");
            } else {
                errorAlert(result.message);
                restoreButton();
            }
        },
        error: function () {
            errorAlert("An error occurred, try again or contact support.");
            restoreButton();
        }
    });
}

function contribute() {

    var $submitBtn = $("#startContributionBtn");

    $submitBtn.prop("disabled", true).text("Processing...");

    let customFieldValues = [];
    $('.dynamic-field').each(function () {
        const value = $(this).val();
        const fieldId = $(this).data('field-id');
        if (fieldId) {
            customFieldValues.push({
                projectCustomFieldId: fieldId,
                value: value
            });
        }
    });

    const data = {
        projectId: $("#projectId").val(),
        fullName: $("#fullName").val(),
        phoneNumber: $("#phoneNumber").val(),
        amount: parseFloat($("#amount").val()),
        customFieldValues: customFieldValues
    };

    if (!data.fullName || !data.phoneNumber || !data.amount) {
        errorAlert("Please fill all required fields.");
        $submitBtn.prop("disabled", false).text("Support Project");
        return;
    }

    $.ajax({
        url: "/api/payment/register-supporter",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify(data),

        success: function (result) {

            let r = result.data;
            contributionId = r.contributionId;

            connection.invoke("JoinContributionGroup", contributionId.toString())
                .then(function () {

                    $("#displayAccountName").text(r.accountName);
                    $("#displayAccountNumber").text(r.accountNumber);
                    $("#displayBank").text(r.bank);
                    $("#displayAmount, #displayAmountWarning").text(r.payable);

                    $("#contributorSection").addClass("d-none");
                    $("#paymentInstructionSection").removeClass("d-none");
                    $("#paymentWaitingSection").removeClass("d-none");

                    // The heading now has one job: say what to do next. Repeating
                    // the project name and its target here told contributors
                    // nothing they had not already decided, and several of them
                    // stopped at this step during the pilot.
                    $("#supportModalTitle").text("Make Payment");
                    $("#supportModalSubtitle").text(
                        "Transfer \u20A6" + r.payable + " to the account below");

                    startPaymentCountdown(300);
                    startPaymentPolling(contributionId);

                })
                .catch(function (err) {
                    errorAlert("Connection failed. Try again.");
                });
        },

        error: function () {
            $submitBtn.prop("disabled", false).text("Try Again");
            errorAlert("Failed to initialize payment.");
        }
    });
}

// Closing the dialog puts it back to the step a contributor starts on.
// Without this a second contribution opens on "Make Payment" with the last
// transfer's amount still under it, and no form to fill in.
$(document).on("hidden.bs.modal", "#support", function () {

    stopPaymentPolling();

    if (paymentTimer) {
        clearInterval(paymentTimer);
        paymentTimer = null;
    }

    $("#paymentInstructionSection, #paymentWaitingSection, #paymentSuccessSection")
        .addClass("d-none");
    $("#contributorSection").removeClass("d-none");
    $("#paymentStatusMessage").empty();

    $("#supportModalTitle").text($("#supportModalTitle").data("initial"));
    $("#supportModalSubtitle").text($("#supportModalSubtitle").data("initial"));

    $("#startContributionBtn").prop("disabled", false).text("Continue");
});

// Remembered once, so the reset above has something to put back.
$(function () {
    $("#supportModalTitle").data("initial", $("#supportModalTitle").text().trim());
    $("#supportModalSubtitle").data("initial", $("#supportModalSubtitle").text().trim());
});

function startPaymentCountdown(seconds) {

    let timeLeft = seconds;
    const el = document.getElementById("countdown");

    if (paymentTimer) {
        clearInterval(paymentTimer);
    }

    paymentTimer = setInterval(function () {

        let minutes = Math.floor(timeLeft / 60);
        let secs = timeLeft % 60;

        el.textContent =
            String(minutes).padStart(2, "0") + ":" +
            String(secs).padStart(2, "0");

        if (timeLeft <= 60) {
            el.classList.add("text-danger");
        }

        if (timeLeft <= 0) {

            clearInterval(paymentTimer);
            paymentTimer = null;

            stopPaymentPolling();

            $("#paymentStatusMessage").html(`
                <span class="text-danger">
                    Payment expired. Please try again.
                </span>
            `);

            $("#startContributionBtn")
                .prop("disabled", false)
                .text("Try Again");

            return;
        }

        timeLeft--;

    }, 1000);
}

function startPaymentPolling(contributionId) {

    stopPaymentPolling();

    paymentPoller = setInterval(function () {

        $.ajax({
            url: "/api/payment/check-status?contributionId=" + contributionId,
            method: "GET",
            success: function (res) {
                switch (res.data) {
                    case "Successful":
                        stopPaymentPolling();
                        stopPaymentTimer();

                        $("#paymentStatusMessage").html(`
                            <span class="text-success">
                                Payment confirmed...
                            </span>
                        `);

                        setTimeout(() => {
                            location.reload();
                        }, 1500);

                        break;

                    case "Failed":
                    case "Expired":
                        stopPaymentPolling();
                        stopPaymentTimer();
                        $("#paymentStatusMessage").html(
                            '<span class="text-danger">Payment failed or expired.</span>'
                        );
                        break;

                    case "UnderPaid":
                        stopPaymentPolling();
                        stopPaymentTimer();
                        $("#paymentStatusMessage").html(
                            '<span class="text-danger">Underpayment detected. Refund initiated.</span>'
                        );
                        break;

                    case "OverPaid":
                        stopPaymentPolling();
                        stopPaymentTimer();
                        $("#paymentStatusMessage").html(
                            '<span class="text-danger">Overpayment detected. Refund initiated.</span>'
                        );
                        break;

                    case "Refunded":
                        stopPaymentPolling();
                        stopPaymentTimer();
                        $("#paymentStatusMessage").html(
                            '<span class="text-warning">Payment refunded.</span>'
                        );
                        break;
                }
            },
            error: function () {
                console.log("Polling error...");
            }
        });

    }, 10000);
}

function stopPaymentPolling() {
    if (paymentPoller) {
        clearInterval(paymentPoller);
        paymentPoller = null;
    }
}

function stopPaymentTimer() {
    if (paymentTimer) {
        clearInterval(paymentTimer);
        paymentTimer = null;
    }
}

if (connection) {

    connection.off("PaymentCompleted");

    connection.on("PaymentCompleted", function (response) {

        const status = response.data;

        switch (status) {

            case "Successful":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-success">
                       Payment confirmed...
                    </span>
                `);

                setTimeout(function () {
                    $("#paymentWaitingSection").addClass("d-none");
                    $("#paymentSuccessSection").removeClass("d-none");
                }, 1000);

                setTimeout(function () {
                    $("#support").modal("hide");
                    location.reload();
                }, 3500);

                break;

            case "UnderPaid":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-danger">
                        Underpayment detected. Refund initiated.
                    </span>
                `);

                break;

            case "OverPaid":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-danger">
                        Overpayment detected. Refund initiated.
                    </span>
                `);

                break;

            case "Failed":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-danger">
                        Payment failed.
                    </span>
                `);

                break;

            case "Expired":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-danger">
                        Payment expired.
                    </span>
                `);

                break;

            case "Refunded":

                stopPaymentPolling();
                stopPaymentTimer();

                $("#paymentStatusMessage").html(`
                    <span class="text-warning">
                        Payment refunded.
                    </span>
                `);

                break;
        }
    });
}

document.getElementById('copyLinkBtn')?.addEventListener('click', () => {
    const input = document.getElementById('shareLinkInput');
    input.select();
    document.execCommand('copy');

    const btn = document.getElementById('copyLinkBtn');
    btn.innerHTML = '<i class="ti ti-check"></i>';
    btn.classList.replace('btn-outline-secondary', 'btn-success');

    setTimeout(() => {
        btn.innerHTML = '<i class="ti ti-copy"></i>';
        btn.classList.replace('btn-success', 'btn-outline-secondary');
    }, 2000);
});

document.getElementById('openLinkBtn')?.addEventListener('click', () => {
    window.open(document.getElementById('shareLinkInput').value, '_blank');
});

document.getElementById('copyQrImageBtn')?.addEventListener('click', async () => {
    const qrImage = document.getElementById('qrCodeImage');
    const button = document.getElementById('copyQrImageBtn');

    if (!qrImage.src || !qrImage.src.startsWith('data:image')) {
        errorAlert("QR code is still loading...");
        return;
    }

    try {
        const response = await fetch(qrImage.src);
        const blob = await response.blob();

        await navigator.clipboard.write([
            new ClipboardItem({ 'image/png': blob })
        ]);

        button.innerHTML = '<i class="ti ti-check"></i>';
        button.classList.replace('btn-outline-success', 'btn-success');

        setTimeout(() => {
            button.innerHTML = '<i class="ti ti-qrcode"></i>';
            button.classList.replace('btn-success', 'btn-outline-success');
        }, 2000);

    } catch (err) {
        console.error(err);
        errorAlert("Copy failed. Try again or screenshot the QR.");
    }
});

function forgotPassword() {
    var $submitBtn = $("#forgot_submit_btn");
    $submitBtn.prop("disabled", true).text("Please wait...");
    var email = $('#email').val().trim();

    if (!email) {
        errorAlert("Email is required.");
        $submitBtn.prop("disabled", false).text("Submit");
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Account/ForgotPassword',
        dataType: 'json',
        data: { email: email },
        success: function (result) {
            $submitBtn.prop("disabled", false).text("Submit");

            if (!result.isError) {
                newSuccessAlert(result.msg, result.returnUrl || "/Account/Login");
            } else {
                errorAlert(result.msg);
            }
        },
        error: function () {
            $submitBtn.prop("disabled", false).text("Submit");
            errorAlert("An error occurred. Please try again.");
        }
    });
}

function resetPassword() {
    var $submitBtn = $("#reset_submit_btn");
    $submitBtn.prop("disabled", true).text("Please wait...");
    var token = $('#token').val().trim();
    var email = $('#email').val().trim(); 
    var password = $('#password').val();
    var confirmPassword = $('#confirmPassword').val();

    if (!token) {
        errorAlert("Invalid or missing reset token.");
        $submitBtn.prop("disabled", false).text("Reset Password");
        return;
    }

    if (!email) {
        errorAlert("Invalid request.");
        $submitBtn.prop("disabled", false).text("Reset Password");
        return;
    }

    if (!password) {
        errorAlert("Password is required.");
        $submitBtn.prop("disabled", false).text("Reset Password");
        return;
    }

    if (!confirmPassword) {
        errorAlert("Please confirm your password.");
        $submitBtn.prop("disabled", false).text("Reset Password");
        return;
    }

    if (password !== confirmPassword) {
        errorAlert("Passwords do not match.");
        $submitBtn.prop("disabled", false).text("Reset Password");
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Account/ResetPassword',
        dataType: 'json',
        data: {
            token: token,
            email: email,
            password: password,
            confirmPassword: confirmPassword
        },
        success: function (result) {
            $submitBtn.prop("disabled", false).text("Reset Password");

            if (!result.isError) {
                var url = result.returnUrl;
                successAlertWithRedirect(result.msg, url);
            } else {
                errorAlert(result.msg);
            }
        },
        error: function () {
            $submitBtn.prop("disabled", false).text("Reset Password");
            errorAlert("An error occurred. Please try again.");
        }
    });
}
function comment() {
    var $submitBtn = $("#submit_btn");
    var originalText = $submitBtn.text();
    $submitBtn.prop("disabled", true).text("Please wait...");
    function restoreButton() {
        $submitBtn.prop("disabled", false).text(originalText);
    }
    var data = {};
    data.FullName = $('#yourFullName').val();
    data.Comment = $('#addComments').val();
    data.ProjectId = $('#messageId').val();
    if (!data.FullName) {
        errorAlert("Name  is required.");
        restoreButton();
        return;
    }
    if (!data.Comment) {
        errorAlert("Comment is required.");
        restoreButton();
        return;
    }

    $.ajax({
        type: 'POST',
        url: '/Guest/Comments',
        dataType: 'json',
        data:
        {
            commentContents: JSON.stringify(data)
        },
        success: function (result) {
            if (!result.isError) {
                var url = window.location.href;
                successAlertWithRedirect(result.msg, url);
            } else {
                errorAlert(result.msg);
                restoreButton();
            }
        },
        error: function (ex) {
            errorAlert("An error has occurred, try again. Please contact support if the error persists.");
            restoreButton();
        }
    });
}
// --- "Show on Payment List" ----------------------------------------------
// The picker offers whatever extra fields the form has right now, so it is
// rebuilt as rows are typed into, added or removed. The first text box in each
// row is the field's name, whichever form the row belongs to.
function refreshPaymentListOptions($select, wanted) {
    var $container = $('#' + $select.data('fields-container'));
    var current = wanted !== undefined ? wanted : $select.val();
    var names = [];

    $container.children().each(function () {
        var name = $.trim($(this).find('input[type=text]').first().val() || '');
        if (name && names.indexOf(name) < 0) names.push(name);
    });

    $select.find('option:not(:first)').remove();
    names.forEach(function (name) {
        $select.append($('<option>').val(name).text(name));
    });

    $select.val(names.indexOf(current) >= 0 ? current : '');
}

function setPaymentListField(prefix, name) {
    refreshPaymentListOptions($('#' + prefix + 'paymentListField'), name || '');
}

// "" means the amount; sent as such so the server can tell it from "not sent".
function readPaymentListField(prefix) {
    return $('#' + prefix + 'paymentListField').val() || '';
}

// Said before the form is filled in, not after it is sent.
$(document).on('show.bs.modal', '#add_project', function () {
    var $note = $('#accountFeeNote').addClass('d-none').text('');

    $.getJSON('/Project/AccountFee', function (quote) {
        if (quote && quote.note) {
            $note.text(quote.note)
                .toggleClass('alert-warning', quote.paidFrom === 'Owed')
                .toggleClass('alert-info', quote.paidFrom !== 'Owed')
                .removeClass('d-none');
        }
    });
});

$(function () {
    $('.payment-list-field').each(function () {
        var $select = $(this);
        var container = document.getElementById($select.data('fields-container'));
        if (!container) return;

        new MutationObserver(function () { refreshPaymentListOptions($select); })
            .observe(container, { childList: true, subtree: true });
        $(container).on('input', function () { refreshPaymentListOptions($select); });
    });
});

function addCustomField() {
    $('#customFieldsContainer').append(`
        <div class="row g-2 mb-2 custom-field-row">
            <div class="col-md-5">
                <input type="text" class="form-control field-name"
                       placeholder="Field name (e.g. Room Number)">
            </div>
            <div class="col-md-4">
                <select class="form-control field-type">
                    <option value="text">Text</option>
                    <option value="number">Number</option>
                    <option value="email">Email</option>
                </select>
            </div>
            <div class="col-md-2">
                <div class="form-check mt-2">
                    <input class="form-check-input field-required" type="checkbox">
                    <label class="form-check-label">Required</label>
                </div>
            </div>
            <div class="col-md-1">
                <button type="button" class="btn btn-sm btn-danger"
                        onclick="$(this).closest('.custom-field-row').remove()">×</button>
            </div>
        </div>
    `);
}

function verifyAccount() {
    let accountNumberInput = $('#accountNumber');
    let bankCodeInput = $('#bankCode');
    let accountHolderInput = $('#accountHolderName');

    const accountNumber = accountNumberInput.val().trim();
    const bankCode = bankCodeInput.val();

    accountHolderInput.val('');
    accountHolderInput.next('.verificationMessage').remove();
    let accountVerified = false;

    if (accountNumber.length !== 10 || !bankCode) {
        return;
    }

    accountHolderInput.after(
        '<small class="verificationMessage text-muted">Verifying account...</small>'
    );

    $.ajax({
        url: '/api/payment/resolve-account',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            accountNumber,
            bankCode
        }),
        success: function (response) {
            accountHolderInput.next('.verificationMessage').remove();
            if (!response.isError) {
                var data = response.data;
                if (data?.accountName) {
                    accountHolderInput.val(data.accountName);
                    $('#sessionId').val(data.sessionId);
                    accountHolderInput.after(
                        '<small class="verificationMessage text-success"><strong>✓ Verified</strong></small>'
                    );
                    accountVerified = true;
                } else {
                    accountHolderInput.after(
                        '<small class="verificationMessage text-danger"><strong>✗ Invalid account number or bank</strong></small>'
                    );
                }
            } else {
                accountHolderInput.next('.verificationMessage').remove();
                accountHolderInput.after(
                    '<small class="verificationMessage text-danger"><strong>✗ Verification failed. Check connection.</strong></small>'
                );
            }
        },
        error: function () {
            accountHolderInput.next('.verificationMessage').remove();
            accountHolderInput.after(
                '<small class="verificationMessage text-danger"><strong>✗ Verification failed. Check connection.</strong></small>'
            );
        }
    });
}

// ---------------------------------------------------------------
// KYC — confirm the phone before spending a verification.
//
// The OTP goes to whatever number was registered against the BVN or NIN,
// which for a lot of people is a SIM they no longer carry. Asking first
// costs nothing; finding out after the call has already been charged does.
// A BVN and a NIN are often registered to different numbers, so offering
// the other one is usually the fix.
// ---------------------------------------------------------------

function otherKycType(type) {
    return type === 'NIN' ? 'BVN' : 'NIN';
}

function confirmKycPhone() {
    var identityType = $("#kycType").val()?.trim() || "";
    var identityNumber = $("#kycNumber").val()?.trim() || "";

    if (!identityType) {
        errorAlert("Please select BVN or NIN");
        return;
    }
    if (!identityNumber) {
        errorAlert("Please enter your BVN or NIN number");
        return;
    }

    var other = otherKycType(identityType);

    $("#kycConfirmType").text(identityType);
    $("#kycSwitchTarget").text(other);
    $("#kycSuggestType").text(other);
    $("#kycPhoneInactiveNote").hide();

    $("#kycStep1").hide();
    $("#kycStepConfirm").show();
}

$(function () {
    // A — the number is live, so it is worth sending the code.
    $("#kycPhoneYes").on("click", function () {
        $("#kycStepConfirm").hide();
        $("#kycStep1").show();
        sendOtpBtn();
    });

    // B — switch to the other ID, which may carry a different number.
    $("#kycPhoneSwitch").on("click", function () {
        var next = otherKycType($("#kycType").val()?.trim() || "");

        $("#kycType").val(next).trigger("change");
        $("#kycNumber").val("").attr("placeholder", "Enter " + next);

        $("#kycStepConfirm").hide();
        $("#kycStep1").show();
        $("#kycNumber").trigger("focus");
    });

    // C — not reachable. Explain why we still need an ID, then point at the other one.
    $("#kycPhoneNo").on("click", function () {
        $("#kycPhoneInactiveNote").show();
    });

    $("#kycConfirmBack").on("click", function () {
        $("#kycStepConfirm").hide();
        $("#kycStep1").show();
    });
});

function sendOtpBtn() {
    $("#sendOtpBtn").prop("disabled", true).text("Sending OTP...");

    var identityType = $("#kycType").val()?.trim() || "";
    var identityNumber = $("#kycNumber").val()?.trim() || "";
    var userId = $("#userId").val()?.trim() || "";

    if (!identityType) {
        errorAlert("Please select BVN or NIN");
        return;
    }
    if (!identityNumber) {
        errorAlert("Please enter your BVN or NIN number");
        return;
    }

    $.ajax({
        url: '/Kyc/InitiateKyc',
        type: 'POST',
        contentType: "application/json",
        data: JSON.stringify({
            identityType: identityType,
            identityNumber: identityNumber,
            userId: userId
        }),
        success: function (response) {
            if (!response.isError) {
                $("#sendOtpBtn").prop("disabled", false).text("Continue");
                $("#identityId").val(response.data);

                $("#kycStep1").hide();
                $("#kycStep2").show();
                var message = response.msg || "OTP sent successfully. Please check your phone.";
                $("#otpMessage").text(message);
                startResendTimer(60);
            } else {
                $("#sendOtpBtn").prop("disabled", false).text("Continue");
                errorAlert(response.msg || "Failed to send OTP");
            }
        },
        error: function () {
            $("#sendOtpBtn").prop("disabled", false).text("Continue");
        }
    });
}

$(function () {
    $("#kycType").select2();

    $("#verifyOtpBtn").on("click", function () {

        const otp = $("#otpCode").val()?.trim() || "";
        const identityId = $("#identityId").val()?.trim() || "";
        const identityType = $("#kycType").val()?.trim() || "";
        const userId = $("#userId").val()?.trim() || "";

        if (!otp) {
            errorAlert("Please enter the OTP");
            return;
        }

        if (!identityId) {
            errorAlert("Verification session expired. Please start over.");
            $("#kycStep2").hide();
            $("#kycStep1").show();
            return;
        }

        const $btn = $(this);
        $btn.text("Verifying...").prop("disabled", true);

        fetch("/Kyc/ValidateKyc", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                identityId: identityId,
                identityType: identityType,
                otp: otp,
                userId: userId
            })
        })
            .then(res => res.json())
            .then(data => {

                if (!data.isError) {
                    successAlert(data.msg);  
                    setTimeout(() => location.reload(), 1500);
                } else {
                    errorAlert(data.msg || "Verification failed");
                }
            })
            .catch(err => {
                errorAlert("Something went wrong. Please try again.");
            })
            .finally(() => {
                $btn.text("Verify OTP").prop("disabled", false);
            });
    });

    // The charge is the server's to decide, so the preview asks for the
    // published table rather than keeping a second copy of it here. Until it
    // arrives the Payable box stays empty — a guessed figure that disagrees
    // with the one we then ask for is worse than no figure at all.
    let chargeBands = null;

    function chargeOn(amount) {
        if (!chargeBands) return null;

        const band = chargeBands.find(b =>
            amount >= b.minAmount && (b.maxAmount === null || amount <= b.maxAmount));

        if (!band) return null;

        return band.bankFlat + (amount * band.bankPercentage) + band.apparcusFlat;
    }

    function refreshPayable() {
        const amount = parseFloat($("#amount").val()) || 0;
        const charge = amount > 0 ? chargeOn(amount) : null;

        if (charge === null) {
            $("#payable").val("");
            return;
        }

        // The kobo the server adds to keep two identical contributions apart is
        // only settled when the virtual account is opened, so this is the
        // amount before it — the payment screen shows the exact figure.
        $("#payable").val((amount + charge).toFixed(2));
    }

    if ($("#amount").length) {
        $.getJSON("/api/payment/pricing", function (result) {
            chargeBands = result?.data?.bands ?? null;
            refreshPayable();
        });

        $("#amount").on("input", refreshPayable);
    }
});

function startResendTimer(seconds) {
    let timeLeft = seconds;
    const $timer = $("#resendTimer");

    const interval = setInterval(() => {
        timeLeft--;
        $timer.text(`Resend OTP in ${timeLeft}s`);

        if (timeLeft <= 0) {
            clearInterval(interval);
            $timer.html('<a href="#" class="text-primary" id="resendOtpLink">Resend OTP</a>');

            $("#resendOtpLink").on("click", function (e) {
                e.preventDefault();
                $("#sendOtpBtn").trigger("click");
            });
        }
    }, 1000);
}

function showTransferInstructions(session) {

    $("#supporterId").val(session.supporterId);

    $("#displayAccountName").text(session.accountName);
    $("#displayAccountNumber").text(session.accountNumber);
    $("#displayBank").text(session.bank);
    $("#displayAmount").text(session.amount);

    $("#contributorSection").addClass("d-none");
    $("#paymentInstructionSection").removeClass("d-none");
}

$(document).ready(function () {

    let session = localStorage.getItem("activeContribution");

    if (!session) return;

    let data = JSON.parse(session);

    showTransferInstructions(data);
});

function submitPaymentProof() {
    var $submitBtn = $("#proof_btn");
    $submitBtn.prop("disabled", true).text("Verifying...");

    let file = $("#proofOfPayment")[0].files[0];

    let form = new FormData();
    form.append("file", file);

    $.ajax({
        url: "/api/payment/upload-proof",
        method: "POST",
        data: form,
        processData: false,
        contentType: false,
        success: function (uploadResult) {
            
            let proof = uploadResult.data;
            let session = JSON.parse(localStorage.getItem("activeContribution"));
            let verify = {
                ProjectId: parseInt($("#projectId").val()),
                SupporterId: parseInt($("#supporterId").val()),
                PaymentReference: $("#paymentReference").val(),
                ProofOfPayment: proof
            };
           let verifyData = JSON.stringify(verify);
            $.ajax({
                url: "/api/payment/verify-transfer",
                method: "POST",
                data: verifyData,
                contentType: "application/json", 
                success: function (result) {
                    if (!result.isError) {
                        localStorage.removeItem("activeContribution");
                        successAlert(result.msg);
                        location.reload();
                    } else {
                        errorAlert(result.msg);
                        $submitBtn.prop("disabled", false).text("Support Project");
                    }
                },
                error: function (xhr) {
                    errorAlert("Error occurred please contact admin.");
                    $submitBtn.prop("disabled", false).text("Support Project");
                }
            });

        },
        error: function (xhr) {
            errorAlert("Error occurred please contact admin.");
            $submitBtn.prop("disabled", false).text("Support Project");
        }
    });
}
function rejectAcceptTransfer(isApprove) {
    var btnId = isApprove ? '#aprrove_btn' : '#reject_btn';
    var $submitBtn = $(btnId);
    $submitBtn.prop("disabled", true).text("Pleas wait...");
    var projectId = $("#projectId").val();
    var supporterId = $("#supporterId").val();
    $.ajax({
        url: "/Project/ApproveTransfer",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify({ supporterId: supporterId, projectId: projectId, isApprove: isApprove }),
        success: function (result) {
            if (!result.isError) {
                successAlert("Payment approved");
                location.reload();
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Support Project");
            }
        },
        error: function (xhr) {
            errorAlert("Error occurred please contact admin.");
            $submitBtn.prop("disabled", false).text("Support Project");
        }
    });
}
function showProof(path) {

    const img = document.getElementById("proofImage");
    const pdf = document.getElementById("proofPdf");
    const noProof = document.getElementById("noProofText");
    const download = document.getElementById("downloadProof");

    img.classList.add("d-none");
    pdf.classList.add("d-none");
    noProof.classList.add("d-none");

    if (!path) {
        noProof.classList.remove("d-none");
        download.style.display = "none";
        return;
    }

    download.href = path;
    download.style.display = "inline-block";

    const extension = path.split('.').pop().toLowerCase();

    if (extension === "pdf") {
        pdf.src = path;
        pdf.classList.remove("d-none");
    }
    else {
        img.src = path;
        img.classList.remove("d-none");
    }
}

function withdraw() {
    var $submitBtn = $("#withdrawBtn");

    var $consentBox = $("#withdrawConsentBox");
    if (!$consentBox.hasClass("d-none") && !$("#withdrawConsentCheckbox").is(":checked")) {
        errorAlert("Please confirm you understand the fee deduction before withdrawing.");
        return;
    }

    $submitBtn.prop("disabled", true).text("Please wait...");
    var bankCode = $("#bankCode").val();
    var accountNumber = $("#accountNumber").val();
    var accountHolderName = $("#accountHolderName").val();
    var withdrawAmount = $("#withdrawAmount").val();
    var transactionPin = $("#transactionPin").val();
    var projectId = $("#withdrawProjectId").val();
    var sessionId = $("#sessionId").val();
    var consentToShortfall = $("#withdrawConsentCheckbox").is(":checked");

    $.ajax({
        url: "/api/payment/withdraw",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify({ projectId: projectId, amount: withdrawAmount, accountNumber: accountNumber, bankCode: bankCode, sessionId: sessionId, transactionPin: transactionPin, accountName: accountHolderName, consentToShortfall: consentToShortfall }),
        success: function (result) {
            if (!result.isError) {
                var note = result.data && result.data.note ? result.data.note : "Withdrawal done!";
                successAlert(note);
                location.reload();
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Withdraw");
            }
        },
        error: function (xhr) {
            errorAlert("Error occurred please contact admin.");
            $submitBtn.prop("disabled", false).text("Withdraw");
        }
    });
}

let withdrawInfo = {
    walletBalance: 0,
    freeWithdrawalsRemaining: 0,
    beadsBalance: 0,
    withdrawalFeeBeads: 15
};

function loadWithdrawalInfo() {
    var projectId = $("#withdrawProjectId").val();

    $.ajax({
        url: "/api/payment/withdrawal-info",
        method: "GET",
        data: { projectId: projectId },
        success: function (result) {
            if (!result.isError) {
                withdrawInfo = result.data;
                $("#availableBalanceDisplay").text(
                    withdrawInfo.walletBalance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                );
                updateWithdrawAmountPreview();
            }
        },
        error: function () {
            errorAlert("Could not load withdrawal info.");
        }
    });
}

$(document).on("click", "#withdrawAllBtn", function () {
    if (!withdrawInfo.walletBalance) return;
    $("#withdrawAmount").val(withdrawInfo.walletBalance.toFixed(2));
    updateWithdrawAmountPreview();
});

function updateWithdrawAmountPreview() {
    var $preview = $("#amountPreview");
    var $beadsPrompt = $("#buyBeadsPromptFromWithdraw");
    var $consentBox = $("#withdrawConsentBox");
    var $consentCheckbox = $("#withdrawConsentCheckbox");
    var amount = parseFloat($("#withdrawAmount").val());

    if (!amount || amount <= 0) {
        $preview.hide();
        $beadsPrompt.addClass("d-none");
        $consentBox.addClass("d-none");
        $consentCheckbox.prop("checked", false);
        return;
    }

    if (withdrawInfo.freeWithdrawalsRemaining > 0) {
        $preview.hide();
        $beadsPrompt.addClass("d-none");
        $consentBox.addClass("d-none");
        return;
    }

    if (withdrawInfo.beadsBalance >= withdrawInfo.withdrawalFeeBeads) {
        $preview.hide();
        $beadsPrompt.addClass("d-none");
        $consentBox.addClass("d-none");
        return;
    }

    $beadsPrompt.removeClass("d-none");

    var fee = withdrawInfo.withdrawalFeeBeads;
    var totalDebit = amount + fee;
    var exceedsBalance = totalDebit > withdrawInfo.walletBalance;

    $preview.show();
    $preview.toggleClass("text-danger", false);
    $preview.toggleClass("text-muted", true);

    if (exceedsBalance) {
        var shortfall = fee - (withdrawInfo.walletBalance - amount);
        var actualTransfer = amount - shortfall;
        if (actualTransfer > 0) {
            $preview.html(
                "The ₦" + fee + " charge will be deducted from this withdrawal — you'll receive ₦" +
                actualTransfer.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + "."
            );
            $consentBox.removeClass("d-none");
        } else {
            $preview.toggleClass("text-danger", true);
            $preview.toggleClass("text-muted", false);
            $preview.html("This balance is too low to cover the ₦" + fee + " withdrawal charge.");
            $consentBox.addClass("d-none");
        }
    } else {
        $preview.html(
            "Withdrawing ₦" + totalDebit.toLocaleString() + " on a ₦" + withdrawInfo.walletBalance.toLocaleString() +
            " balance (includes the ₦" + fee + " bank charge)."
        );
        $consentBox.addClass("d-none");
    }
}

$(function () {
    $("#withdrawAmount").on("input", updateWithdrawAmountPreview);
});

function toggleField(inputId, iconId) {
    var input = document.getElementById(inputId);
    var icon = document.getElementById(iconId);
    if (input.type === "password") {
        input.type = "text";
        icon.classList.remove("bi-eye-slash");
        icon.classList.add("bi-eye");
    } else {
        input.type = "password";
        icon.classList.remove("bi-eye");
        icon.classList.add("bi-eye-slash");
    }
}

function setTransactionPin() {
    $("#setPinBtn").prop("disabled", true).text("Setting PIN...");

    var transactionPin = $("#setTransactionPin").val();

    if (!transactionPin) {
        $("#setPinBtn").prop("disabled", false).text("Create");
        errorAlert("Please enter a 6-digit transaction PIN");
        return;
    }

    if (transactionPin.length !== 6) {
        $("#setPinBtn").prop("disabled", false).text("Create");
        errorAlert("Transaction PIN must be 6 digits");
        return;
    }

    $.ajax({
        url: '/Kyc/SetTransactionPin',
        type: 'POST',
        contentType: "application/json",
        data: JSON.stringify({
            transactionPin: transactionPin
        }),
        success: function (response) {
            if (!response.isError) {
                $("#setPinBtn").prop("disabled", false).text("Create");
                $("#setTransactionPin").val("");
                successAlert("Transaction PIN set successfully.");
                location.reload();
            } else {
                $("#setPinBtn").prop("disabled", false).text("Create");
                errorAlert(response.msg || "Failed to set transaction PIN");
            }
        },
        error: function () {
            $("#setPinBtn").prop("disabled", false).text("Create");
            errorAlert("An error occurred while setting the transaction PIN.");
        }
    });
}
$('#moveBeadsModal').on('shown.bs.modal', function () {
    $("#availableBeadsDisplay").text(currentBeadsBalance.toFixed(0));
});

$('#buyBeadsModal').on('shown.bs.modal', function () {
    $("#buyBeadsPersonalBalanceDisplay").text(currentPersonalBalance.toFixed(2));
});

$(document).on("click", "#moveAllBeadsBtn", function () {
    $("#beadsMoveAmount").val(currentBeadsBalance);
});

function moveBeads() {
    var $submitBtn = $("#moveBeadsBtn");
    $submitBtn.prop("disabled", true).text("Processing...");

    var amount = parseFloat($("#beadsMoveAmount").val());

    if (!amount || amount <= 0) {
        errorAlert("Enter a valid amount of Beads to move.");
        $submitBtn.prop("disabled", false).text("Move Beads");
        return;
    }

    $.ajax({
        url: "/api/beads/move",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify({ amount: amount }),
        success: function (result) {
            if (!result.isError) {
                successAlert(result.msg || "Beads moved successfully.");
                setTimeout(function () { location.reload(); }, 1500);
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Move Beads");
            }
        },
        error: function () {
            errorAlert("An error occurred. Please try again.");
            $submitBtn.prop("disabled", false).text("Move Beads");
        }
    });
}

function buyBeads() {
    var $submitBtn = $("#buyBeadsBtn");
    $submitBtn.prop("disabled", true).text("Processing...");

    $.ajax({
        url: "/api/beads/buy",
        method: "POST",
        contentType: "application/json",
        success: function (result) {
            if (!result.isError) {
                successAlert(result.msg || "Beads purchased successfully.");
                setTimeout(function () { location.reload(); }, 1500);
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Buy 50 Beads for ₦50");
            }
        },
        error: function () {
            errorAlert("An error occurred. Please try again.");
            $submitBtn.prop("disabled", false).text("Buy 50 Beads for ₦50");
        }
    });
}

$(document).on("click", "#goToBuyBeadsBtn", function () {
    var withdrawModalEl = document.getElementById('withdrawModal');
    var withdrawModal = bootstrap.Modal.getInstance(withdrawModalEl);

    withdrawModalEl.addEventListener('hidden.bs.modal', function openBuyBeads() {
        var buyBeadsModal = new bootstrap.Modal(document.getElementById('buyBeadsModal'));
        buyBeadsModal.show();
        withdrawModalEl.removeEventListener('hidden.bs.modal', openBuyBeads);
    });

    withdrawModal.hide();
});

let personalWithdrawInfo = { personalBalance: 0, beadsBalance: 0, withdrawalFeeBeads: 15 };

$('#personalWithdrawModal').on('shown.bs.modal', function () {
    $('#personalWithdrawBankCode').select2({ dropdownParent: $('#personalWithdrawModal'), width: '100%' });
    loadPersonalWithdrawalInfo();
});

function loadPersonalWithdrawalInfo() {
    $.ajax({
        url: "/api/payment/personal-withdrawal-info",
        method: "GET",
        success: function (result) {
            if (!result.isError) {
                personalWithdrawInfo = result.data;
                $("#personalWithdrawBalanceDisplay").text(
                    personalWithdrawInfo.personalBalance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                );
                updatePersonalWithdrawPreview();
            }
        },
        error: function () {
            errorAlert("Could not load Personal Account info.");
        }
    });
}

$(document).on("click", "#personalWithdrawAllBtn", function () {
    if (!personalWithdrawInfo.personalBalance) return;
    $("#personalWithdrawAmount").val(personalWithdrawInfo.personalBalance.toFixed(2));
    updatePersonalWithdrawPreview();
});

function updatePersonalWithdrawPreview() {
    var $preview = $("#personalWithdrawAmountPreview");
    var $consentBox = $("#personalWithdrawConsentBox");
    var amount = parseFloat($("#personalWithdrawAmount").val());

    if (!amount || amount <= 0) {
        $preview.hide();
        $consentBox.addClass("d-none");
        return;
    }

    if (personalWithdrawInfo.beadsBalance >= personalWithdrawInfo.withdrawalFeeBeads) {
        $preview.hide();
        $consentBox.addClass("d-none");
        return;
    }

    var fee = personalWithdrawInfo.withdrawalFeeBeads;
    var totalDebit = amount + fee;
    var exceedsBalance = totalDebit > personalWithdrawInfo.personalBalance;

    $preview.show();
    $preview.toggleClass("text-danger", false);
    $preview.toggleClass("text-muted", true);

    if (exceedsBalance) {
        var shortfall = fee - (personalWithdrawInfo.personalBalance - amount);
        var actualTransfer = amount - shortfall;
        if (actualTransfer > 0) {
            $preview.html("The ₦" + fee + " charge will be deducted — you'll receive ₦" +
                actualTransfer.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ".");
            $consentBox.removeClass("d-none");
        } else {
            $preview.toggleClass("text-danger", true).toggleClass("text-muted", false);
            $preview.html("Balance too low to cover the ₦" + fee + " charge.");
            $consentBox.addClass("d-none");
        }
    } else {
        $preview.html("Withdrawing ₦" + totalDebit.toLocaleString() + " (includes ₦" + fee + " bank charge).");
        $consentBox.addClass("d-none");
    }
}

$(function () {
    $("#personalWithdrawAmount").on("input", updatePersonalWithdrawPreview);
});

function verifyPersonalWithdrawAccount() {
    var accountNumber = $('#personalWithdrawAccountNumber').val().trim();
    var bankCode = $('#personalWithdrawBankCode').val();
    var $holder = $('#personalWithdrawAccountHolderName');

    $holder.val('');
    if (accountNumber.length !== 10 || !bankCode) return;

    $.ajax({
        url: '/api/payment/resolve-account',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ accountNumber, bankCode }),
        success: function (response) {
            if (!response.isError && response.data?.accountName) {
                $holder.val(response.data.accountName);
                $('#personalWithdrawSessionId').val(response.data.sessionId);
            }
        }
    });
}

function personalWithdraw() {
    var $submitBtn = $("#personalWithdrawBtn");

    var $consentBox = $("#personalWithdrawConsentBox");
    if (!$consentBox.hasClass("d-none") && !$("#personalWithdrawConsentCheckbox").is(":checked")) {
        errorAlert("Please confirm you understand the fee deduction before withdrawing.");
        return;
    }

    $submitBtn.prop("disabled", true).text("Please wait...");

    $.ajax({
        url: "/api/payment/withdraw-personal",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify({
            amount: $("#personalWithdrawAmount").val(),
            accountNumber: $("#personalWithdrawAccountNumber").val(),
            bankCode: $("#personalWithdrawBankCode").val(),
            accountName: $("#personalWithdrawAccountHolderName").val(),
            transactionPin: $("#personalWithdrawTransactionPin").val(),
            sessionId: $("#personalWithdrawSessionId").val(),
            consentToShortfall: $("#personalWithdrawConsentCheckbox").is(":checked")
        }),
        success: function (result) {
            if (!result.isError) {
                successAlert(result.data?.note || "Withdrawal successful!");
                setTimeout(function () { location.reload(); }, 1500);
            } else {
                errorAlert(result.msg);
                $submitBtn.prop("disabled", false).text("Withdraw");
            }
        },
        error: function () {
            errorAlert("Error occurred. Please contact admin.");
            $submitBtn.prop("disabled", false).text("Withdraw");
        }
    });
}