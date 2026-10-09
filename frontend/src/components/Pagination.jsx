export default function Pagination({ page, totalPages, total, label = "items", onPageChange }) {
  return (
    <div className="pagination">
      <button type="button" className="pagination-button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </button>
      <span className="pagination-info">Page {page} of {totalPages} · {total} {label}</span>
      <button type="button" className="pagination-button" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
        Next
      </button>
    </div>
  );
}
