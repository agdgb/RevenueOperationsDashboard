/**
 * Initializes Ethiopian Date Pickers by class with period enforcement and optional pre-selection.
 * @param {string} classSelector - The class name (without the dot).
 * @param {object} min - {year: 2018, month: 4, day: 1}
 * @param {object} max - {year: 2018, month: 4, day: 30}
 * @param {object} selected - {year: 2018, month: 4, day: 15} (Optional)
 */
function initETDateByClass(classSelector, min, max, selected) {
    var calendar = $.calendars.instance('ethiopian', 'am');

    // Reference the centralized server date from the partial
    var serverToday = calendar.newDate(
        parseInt(window.SERVER_DATE.year),
        parseInt(window.SERVER_DATE.month),
        parseInt(window.SERVER_DATE.day)
    );
    console.log('serverToday', serverToday);
    $(`.${classSelector}`).each(function () {
        var currentInput = $(this);

        // Convert plain objects to Calendar Date objects
        var minLimit = min ? calendar.newDate(parseInt(min.year), parseInt(min.month), parseInt(min.day || 1)) : null;

        // ENFORCEMENT: If max is null, default to serverToday to disable future dates
        var maxLimit = max ? calendar.newDate(parseInt(max.year), parseInt(max.month), parseInt(max.day || 30)) : serverToday;

        var selectedVal = selected ? calendar.newDate(parseInt(selected.year), parseInt(selected.month), parseInt(selected.day)) : null;

        // Force clear any Gregorian value (like 2026-01-13) before initializing
        currentInput.val('');

        currentInput.calendarsPicker({
            calendar: calendar,
            dateFormat: 'yyyy-mm-dd',
            minDate: minLimit,
            maxDate: maxLimit,
            prevText: '<i class="bx bx-chevron-left"></i>',
            nextText: '<i class="bx bx-chevron-right"></i>',
            todayText: 'ዛሬ',
            clearText: 'አጥፋ',
            closeText: 'ዝጋ',
            showAnim: 'fadeIn',
            onSelect: function (dates) {
                if (dates.length > 0) {
                    setTimeout(function () {
                        currentInput.calendarsPicker('hide');
                    }, 100);
                }
            },
            onShow: function () {
                $(this).prop('readonly', true);
            }
        });

        // Priority Logic: 1. Selected Date -> 2. Smart Server Today -> 3. Start of Period
        if (selectedVal) {
            currentInput.calendarsPicker('setDate', selectedVal);
        } else {
            // Check if Server Today falls within the provided min/max range
            if (minLimit && maxLimit) {
                if (serverToday.toJD() >= minLimit.toJD() && serverToday.toJD() <= maxLimit.toJD()) {
                    currentInput.calendarsPicker('setDate', serverToday);
                } else {
                    currentInput.calendarsPicker('setDate', minLimit);
                }
            } else {
                currentInput.calendarsPicker('setDate', serverToday);
            }
        }
    });
}

/**
 * Initializes Ethiopian Date Pickers by ID with period enforcement and optional pre-selection.
 * @param {string} IdSelector - The ID of the input (without the #).
 */
function initETDateById(IdSelector, min, max, selected) {
    var calendar = $.calendars.instance('ethiopian', 'am');

    // Reference the centralized server date from the partial
    var serverToday = calendar.newDate(
        parseInt(window.SERVER_DATE.year),
        parseInt(window.SERVER_DATE.month),
        parseInt(window.SERVER_DATE.day)
    );

    $(`#${IdSelector}`).each(function () {
        var currentInput = $(this);

        var minLimit = min ? calendar.newDate(parseInt(min.year), parseInt(min.month), parseInt(min.day || 1)) : null;

        // ENFORCEMENT: If max is null, default to serverToday to disable future dates
        var maxLimit = max ? calendar.newDate(parseInt(max.year), parseInt(max.month), parseInt(max.day || 30)) : serverToday;

        var selectedVal = selected ? calendar.newDate(parseInt(selected.year), parseInt(selected.month), parseInt(selected.day)) : null;

        // Force clear any Gregorian value before initializing
        currentInput.val('');

        currentInput.calendarsPicker({
            calendar: calendar,
            dateFormat: 'yyyy-mm-dd',
            minDate: minLimit,
            maxDate: maxLimit,
            prevText: '<i class="bx bx-chevron-left"></i>',
            nextText: '<i class="bx bx-chevron-right"></i>',
            todayText: 'ዛሬ',
            clearText: 'አጥፋ',
            closeText: 'ዝጋ',
            showAnim: 'fadeIn',
            onSelect: function (dates) {
                if (dates.length > 0) {
                    setTimeout(function () {
                        currentInput.calendarsPicker('hide');
                    }, 100);
                }
            },
            onShow: function () {
                $(this).prop('readonly', true);
            }
        });

        if (selectedVal) {
            currentInput.calendarsPicker('setDate', selectedVal);
        } else {
            if (minLimit && maxLimit) {
                if (serverToday.toJD() >= minLimit.toJD() && serverToday.toJD() <= maxLimit.toJD()) {
                    currentInput.calendarsPicker('setDate', serverToday);
                } else {
                    currentInput.calendarsPicker('setDate', minLimit);
                }
            } else {
                currentInput.calendarsPicker('setDate', serverToday);
            }
        }
    });
}
///**
// * Initializes Ethiopian Date Pickers by class with period enforcement and optional pre-selection.
// * @param {string} classSelector - The class name (without the dot).
// * @param {object} min - {year: 2018, month: 4, day: 1}
// * @param {object} max - {year: 2018, month: 4, day: 30}
// * @param {object} selected - {year: 2018, month: 4, day: 15} (Optional)
// */
//function initETDateByClass(classSelector, min, max, selected) {
//    var calendar = $.calendars.instance('ethiopian', 'am');

//    $(`.${classSelector}`).each(function () {
//        var currentInput = $(this);

//        // Convert plain objects to Calendar Date objects
//        var minLimit = min ? calendar.newDate(parseInt(min.year), parseInt(min.month), parseInt(min.day || 1)) : null;
//        var maxLimit = max ? calendar.newDate(parseInt(max.year), parseInt(max.month), parseInt(max.day || 30)) : null;
//        var selectedVal = selected ? calendar.newDate(parseInt(selected.year), parseInt(selected.month), parseInt(selected.day)) : null;

//        currentInput.calendarsPicker({
//            calendar: calendar,
//            minDate: minLimit,
//            maxDate: maxLimit,
//            prevText: '<i class="bx bx-chevron-left"></i>',
//            nextText: '<i class="bx bx-chevron-right"></i>',
//            todayText: 'ዛሬ',
//            clearText: 'አጥፋ',
//            closeText: 'ዝጋ',
//            showAnim: 'fadeIn',
//            onSelect: function (dates) {
//                if (dates.length > 0) {
//                    setTimeout(function () {
//                        currentInput.calendarsPicker('hide');
//                    }, 100);
//                }
//            },
//            onShow: function () {
//                $(this).prop('readonly', true);
//            }
//        });

//        // Priority Logic: 1. Selected Date -> 2. Smart Today -> 3. Start of Period
//        if (selectedVal) {
//            currentInput.calendarsPicker('setDate', selectedVal);
//        } else {
//            var today = calendar.today();
//            if (minLimit && maxLimit) {
//                if (today.toJD() >= minLimit.toJD() && today.toJD() <= maxLimit.toJD()) {
//                    currentInput.calendarsPicker('setDate', today);
//                } else {
//                    currentInput.calendarsPicker('setDate', minLimit);
//                }
//            } else {
//                currentInput.calendarsPicker('setDate', today);
//            }
//        }
//    });
//}

///**
// * Initializes Ethiopian Date Pickers by ID with period enforcement and optional pre-selection.
// * @param {string} IdSelector - The ID of the input (without the #).
// */
//function initETDateById(IdSelector, min, max, selected) {
//    var calendar = $.calendars.instance('ethiopian', 'am');

//    $(`#${IdSelector}`).each(function () {
//        var currentInput = $(this);

//        var minLimit = min ? calendar.newDate(parseInt(min.year), parseInt(min.month), parseInt(min.day || 1)) : null;
//        var maxLimit = max ? calendar.newDate(parseInt(max.year), parseInt(max.month), parseInt(max.day || 30)) : null;
//        var selectedVal = selected ? calendar.newDate(parseInt(selected.year), parseInt(selected.month), parseInt(selected.day)) : null;

//        currentInput.calendarsPicker({
//            calendar: calendar,
//            minDate: minLimit,
//            maxDate: maxLimit,
//            prevText: '<i class="bx bx-chevron-left"></i>',
//            nextText: '<i class="bx bx-chevron-right"></i>',
//            todayText: 'ዛሬ',
//            clearText: 'አጥፋ',
//            closeText: 'ዝጋ',
//            showAnim: 'fadeIn',
//            onSelect: function (dates) {
//                if (dates.length > 0) {
//                    setTimeout(function () {
//                        currentInput.calendarsPicker('hide');
//                    }, 100);
//                }
//            },
//            onShow: function () {
//                $(this).prop('readonly', true);
//            }
//        });

//        if (selectedVal) {
//            currentInput.calendarsPicker('setDate', selectedVal);
//        } else {
//            var today = calendar.today();
//            if (minLimit && maxLimit) {
//                if (today.toJD() >= minLimit.toJD() && today.toJD() <= maxLimit.toJD()) {
//                    currentInput.calendarsPicker('setDate', today);
//                } else {
//                    currentInput.calendarsPicker('setDate', minLimit);
//                }
//            } else {
//                currentInput.calendarsPicker('setDate', today);
//            }
//        }
//    });
//}
