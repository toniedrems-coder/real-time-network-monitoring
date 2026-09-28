import { NavLink } from "react-router-dom";

const links = [
  ["/dashboard", "Dashboard"],
  ["/endpoints", "Endpoints"],
  ["/incidents", "Incidents"],
  ["/agents", "Agents"],
  ["/reports", "Reports"],
];

export default function Navbar() {
  return (
    <header className="topbar">
      <div className="brand">
        Network Monitor
      </div>

      <nav aria-label="Main navigation">
        {links.map(([to, label]) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              isActive
                ? "nav-link active"
                : "nav-link"
            }
          >
            {label}
          </NavLink>
        ))}
      </nav>
    </header>
  );
}