document.addEventListener("DOMContentLoaded", () => {
    const toggleButton = document.getElementById("modeToggle");
    const currentMode = localStorage.getItem("theme");

    if (currentMode === "dark") {
        document.body.classList.add("dark-mode");
    }

    toggleButton.addEventListener("click", () => {
        document.body.classList.toggle("dark-mode");
        const newTheme = document.body.classList.contains("dark-mode") ? "dark" : "light";
        localStorage.setItem("theme", newTheme);
    });
});
