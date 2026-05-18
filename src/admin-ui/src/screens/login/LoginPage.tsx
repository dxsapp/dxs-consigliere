/**
 * S0 placeholder login screen. Real form + auth client integration
 * lands in S2 (route guard + form) + S3 (POST /api/admin/auth/login
 * via the auth client). For now this just exists so the 401-redirect
 * target from the API client has somewhere to land.
 */
export function LoginPage() {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        height: "100vh",
        color: "white",
        background: "#0E1116",
      }}
    >
      <div style={{ textAlign: "center" }}>
        <h1 style={{ margin: 0 }}>Consigliere Admin</h1>
        <p style={{ opacity: 0.6 }}>Login form ships in S2.</p>
      </div>
    </div>
  );
}
