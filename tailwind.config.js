/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ["Raleway", "ui-sans-serif", "system-ui", "Segoe UI", "Roboto", "Arial", "Noto Sans", "sans-serif"]
      },
      colors: {
        /* Брендбук DPA */
        dpa: {
          900: "#002852",
          800: "#003971", /* Pantone 662 C — основной */
          700: "#004C97", /* Pantone 2945 C */
          600: "#0066AD",
          500: "#0077C8", /* Pantone 3005 C — акцент */
          400: "#2E9FE8",
          300: "#74C0FC",
          200: "#BEE3F8",
          100: "#E6F1FB",
          50:  "#F2F7FC"
        },
        dpaGray: {
          900: "#1F262B",
          800: "#2C353B",
          700: "#3E4850",
          600: "#525E66", /* основной серый текст */
          500: "#7B848A",
          400: "#A6AFB4",
          300: "#C8CED2",
          200: "#DDE2E6",
          100: "#ECEFF2",
          50:  "#F6F7F9"
        }
      }
    }
  },
  plugins: []
};
