// Keyboard triage: focus the selected row by its position (focusing also scrolls it into view).
// By position, not by element reference: a row that comes back after an undo is a new element.
window.jobRadar = {
    focusRow: index => document.querySelectorAll(".rows > .row")[index]?.focus(),
};
