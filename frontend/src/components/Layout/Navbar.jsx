import { NavLink } from "react-router-dom";

const links = [
  ["/dashboard", "Dashboard"],
  ["/endpoints", "Endpoints"],
  ["/reports", "Reports"],
];

export default function Navbar() {
  return (
    <header className="topbar">
      <div className="brand">Network Monitor</div>
      <nav aria-label="Main navigation">
        {links.map(([to, label]) => (
          <NavLink
            className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}
            key={to}
            to={to}
          >
            {label}
          </NavLink>
        ))}
      </nav>
    </header>
  );
}