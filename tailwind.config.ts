import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "var(--background)",
        foreground: "var(--foreground)",
        // Soft Minimal Palette
        soft: {
          bg: "#F9FAFB", // Very light gray for background
          surface: "#FFFFFF", // Pure white for cards
          primary: "#3B82F6", // Royal Blue
          primaryHover: "#2563EB",
          text: "#1F2937", // Charcoal
          subtext: "#6B7280", // Gray
          border: "#E5E7EB", // Light gray border
        }
      },
      boxShadow: {
        'soft-sm': '0 2px 4px rgba(0, 0, 0, 0.02), 0 1px 2px rgba(0, 0, 0, 0.03)',
        'soft': '0 4px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px -1px rgba(0, 0, 0, 0.02)',
        'soft-md': '0 10px 15px -3px rgba(0, 0, 0, 0.02), 0 4px 6px -2px rgba(0, 0, 0, 0.01)',
        'soft-lg': '0 20px 25px -5px rgba(0, 0, 0, 0.02), 0 10px 10px -5px rgba(0, 0, 0, 0.01)',
        'soft-xl': '0 25px 50px -12px rgba(0, 0, 0, 0.05)',
      }
    },
  },
  plugins: [],
};
export default config;
