const sw = document.getElementById("switch-style"), b = document.body;
if (sw && b) {
  sw.checked = true;
  b.classList.toggle("dark-theme", sw.checked)
  b.classList.toggle("light-theme", !sw.checked)
  
  sw.addEventListener("change", function (){
    b.classList.toggle("dark-theme", this.checked)
    b.classList.toggle("light-theme", !this.checked)
    if (window.localStorage) {
      this.checked ? localStorage.setItem("theme", "dark-theme") : localStorage.setItem("theme", "light-theme")
    }
  })
}