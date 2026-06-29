/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        primary: '#2563EB',
        surface: '#FFFFFF',
        background: '#F3F4F6',
        footer: '#1F2937',
        'card-header': '#DBEAFE',
      },
    },
  },
  plugins: [],
};
