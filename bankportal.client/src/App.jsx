import React, { useEffect, useState } from "react";
import "./App.css";

// Always call API on the SAME origin as the ASP.NET app (https://localhost:7267)
const API_BASE = window.location.origin;

function useField(init = "") {
    const [v, set] = useState(init);
    return { value: v, onChange: e => set(e.target.value), set };
}

// === Validation rules (ASCII only) ===
const NAME_RE = /^[A-Za-z ,.'-]{2,60}$/;    // Full name (letters + ,.'-)
const ID_RE = /^[0-9A-Za-z-]{6,20}$/;       // Alnum + "-"  (6-20)
const ACC_RE = /^[0-9]{8,20}$/;             // Digits only  (8-20)
const USER_RE = /^[a-zA-Z0-9_.-]{3,30}$/;   // a-z A-Z 0-9 _ . -  (3-30)
const PASS_MIN = 8, PASS_MAX = 64;

// --- API helpers ---
async function getCsrf() {
    await fetch(`${API_BASE}/api/auth/csrf`, { credentials: "include" });
}

async function xsrf() {
    const cookies = document.cookie.split(";").map(s => s.trim());
    const hit = cookies.find(x => x.startsWith("XSRF-TOKEN="));
    return hit ? hit.split("=")[1] : "";
}

async function post(path, body) {
    await getCsrf();
    const token = await xsrf();
    const res = await fetch(`${API_BASE}${path}`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token
        },
        credentials: "include",
        body: JSON.stringify(body)
    });

    const text = await res.text();
    let data = {};
    try {
        data = text ? JSON.parse(text) : {};
    } catch {
        // ignore JSON parse error, keep raw text in err.text
    }

    if (!res.ok) {
        const err = new Error(`HTTP ${res.status}`);
        err.status = res.status;
        err.data = data;  // server JSON like { error, details: {...} }
        err.text = text;
        throw err;
    }
    return data;
}

async function get(path) {
    const res = await fetch(`${API_BASE}${path}`, { credentials: "include" });
    const text = await res.text();
    let data = {};
    try {
        data = text ? JSON.parse(text) : {};
    } catch {
        // ignore JSON parse error
    }

    if (!res.ok) {
        const err = new Error(`HTTP ${res.status}`);
        err.status = res.status;
        err.data = data;
        err.text = text;
        throw err;
    }
    return data;
}

// single-field validators (for live feedback)
const rules = {
    fullName: v =>
        NAME_RE.test(v.trim()) ? "" : "2-60 letters; may include spaces , . ' -",
    idNumber: v =>
        ID_RE.test(v.trim()) ? "" : "6-20; letters/digits/hyphen only",
    accountNumber: v =>
        ACC_RE.test(v.trim()) ? "" : "Digits only, 8-20 long",
    username: v =>
        USER_RE.test(v.trim()) ? "" : "3-30; letters/digits/_ . -",
    password: v =>
        v && v.length >= PASS_MIN && v.length <= PASS_MAX
            ? ""
            : `Password must be ${PASS_MIN}-${PASS_MAX} characters`
};

export default function App() {
    const [msg, setMsg] = useState("");
    const [view, setView] = useState("register"); // "register" | "login" | "pay" | "history" | "emp"
    const [isAuthed, setIsAuthed] = useState(false);
    const [isEmployee, setIsEmployee] = useState(false);
    const [currentUser, setCurrentUser] = useState(null);

    // customer fields
    const full_name = useField("");
    const id_number = useField("");
    const account_number = useField("");
    const username = useField("");
    const password = useField("");

    const luser = useField("");
    const lacc = useField("");
    const lpass = useField("");

    const amount = useField("");
    const currency = useField("ZAR");
    const provider = useField("SWIFT");
    const payee_account = useField("");
    const swift_bic = useField("");
    const [payments, setPayments] = useState([]);

    // employee payments view state
    const [empPayments, setEmpPayments] = useState([]);
    const [empStatusFilter, setEmpStatusFilter] = useState("PendingVerification");

    // employee creation form fields
    const empFullName = useField("");
    const empIdNumber = useField("");
    const empAccountNumber = useField("");
    const empUsername = useField("");
    const empPassword = useField("");

    // per-form error maps
    const [regErrs, setRegErrs] = useState({});
    const [logErrs, setLogErrs] = useState({});
    const [empRegErrs, setEmpRegErrs] = useState({});

    // on load: get CSRF and see if already logged in 
    useEffect(() => {
        (async () => {
            try {
                await getCsrf();
                const me = await get("/api/auth/me"); // { id, username, isEmployee }
                if (me && me.id) {
                    setIsAuthed(true);
                    setIsEmployee(!!me.isEmployee);
                    setCurrentUser(me);
                    setView("pay");
                }
            } catch {
                // not logged in
            }
        })();
    }, []);

    // ---- navigation helpers ----
    function goLogin() {
        setMsg("");
        setView("login");
    }

    function goRegister() {
        setMsg("");
        setView("register");
    }

    function goProtected(target) {
        if (!isAuthed) {
            setMsg("Please login first.");
            setView("login");
            return;
        }
        setMsg("");
        setView(target);
    }

    // ----- form-wide validators -----
    function validateRegister(v) {
        const e = {};
        e.fullName = rules.fullName(v.fullName);
        e.idNumber = rules.idNumber(v.idNumber);
        e.accountNumber = rules.accountNumber(v.accountNumber);
        e.username = rules.username(v.username);
        e.password = rules.password(v.password);
        Object.keys(e).forEach(k => {
            if (!e[k]) delete e[k];
        });
        return e;
    }

    function validateLogin(v) {
        const e = {};
        e.username = rules.username(v.username);
        e.accountNumber = rules.accountNumber(v.accountNumber);
        e.password = rules.password(v.password);
        Object.keys(e).forEach(k => {
            if (!e[k]) delete e[k];
        });
        return e;
    }

    // ---- live validation wrappers ----
    const onChangeRegister = {
        fullName: e => {
            full_name.onChange(e);
            setRegErrs(prev => ({ ...prev, fullName: rules.fullName(e.target.value) }));
        },
        idNumber: e => {
            id_number.onChange(e);
            setRegErrs(prev => ({ ...prev, idNumber: rules.idNumber(e.target.value) }));
        },
        accountNumber: e => {
            account_number.onChange(e);
            setRegErrs(prev => ({ ...prev, accountNumber: rules.accountNumber(e.target.value) }));
        },
        username: e => {
            username.onChange(e);
            setRegErrs(prev => ({ ...prev, username: rules.username(e.target.value) }));
        },
        password: e => {
            password.onChange(e);
            setRegErrs(prev => ({ ...prev, password: rules.password(e.target.value) }));
        }
    };

    const onChangeLogin = {
        username: e => {
            luser.onChange(e);
            setLogErrs(prev => ({ ...prev, username: rules.username(e.target.value) }));
        },
        accountNumber: e => {
            lacc.onChange(e);
            setLogErrs(prev => ({ ...prev, accountNumber: rules.accountNumber(e.target.value) }));
        },
        password: e => {
            lpass.onChange(e);
            setLogErrs(prev => ({ ...prev, password: rules.password(e.target.value) }));
        }
    };

    // ---- actions ----
    async function doRegister() {
        setMsg("");
        const payload = {
            fullName: full_name.value.trim(),
            idNumber: id_number.value.trim(),
            accountNumber: account_number.value.trim(),
            username: username.value.trim(),
            password: password.value
        };

        const e = validateRegister(payload);
        setRegErrs(e);
        if (Object.keys(e).length) {
            setMsg("Please fix the highlighted fields.");
            return;
        }

        try {
            await post("/api/auth/register", payload);
            setMsg("Registered. Please login.");
            setView("login");
            setRegErrs({});
        } catch (err) {
            const d = err && err.data;
            if (d && d.details) {
                const mapped = {
                    fullName: d.details.FullName && d.details.FullName[0],
                    idNumber: d.details.IdNumber && d.details.IdNumber[0],
                    accountNumber: d.details.AccountNumber && d.details.AccountNumber[0],
                    username: d.details.Username && d.details.Username[0],
                    password: d.details.Password && d.details.Password[0]
                };
                setRegErrs(prev => ({ ...prev, ...mapped }));
                setMsg("Please fix the highlighted fields.");
                return;
            }
            if (d && d.error) {
                setMsg(d.error);
                return;
            }
            setMsg(err.text || err.message || "Request failed");
        }
    }

    async function doLogin() {
        setMsg("");
        const payload = {
            username: luser.value.trim(),
            accountNumber: lacc.value.trim(),
            password: lpass.value
        };

        const e = validateLogin(payload);
        setLogErrs(e);
        if (Object.keys(e).length) {
            setMsg("Please fix the highlighted fields.");
            return;
        }

        try {
            const data = await post("/api/auth/login", payload); // { ok, user: { id, username, isEmployee } }
            setIsAuthed(true);
            setIsEmployee(!!(data && data.user && data.user.isEmployee));
            setCurrentUser((data && data.user) || null);
            setMsg("Logged in.");
            setView("pay");
            setLogErrs({});
        } catch (err) {
            const d = err && err.data;
            if (d && d.details) {
                const mapped = {
                    username: d.details.Username && d.details.Username[0],
                    accountNumber: d.details.AccountNumber && d.details.AccountNumber[0],
                    password: d.details.Password && d.details.Password[0]
                };
                setLogErrs(prev => ({ ...prev, ...mapped }));
                setMsg("Please fix the highlighted fields.");
                return;
            }
            if (d && d.error) {
                setMsg(d.error);   // e.g. "Invalid credentials"
                return;
            }
            setMsg(err.text || err.message || "Request failed");
        }
    }

    async function doPayment() {
        setMsg("");
        try {
            const idem = crypto.randomUUID();
            const data = await post("/api/payments", {
                amount: amount.value,
                currency: currency.value,
                provider: provider.value,
                payeeAccount: payee_account.value,
                swiftBic: swift_bic.value,
                idempotencyKey: idem
            });
            setMsg(`Payment ${data.payment.id} status ${data.payment.status}`);
        } catch (e) {
            setMsg((e && e.text) || e.message || "Payment failed");
        }
    }

    async function loadPayments() {
        setMsg("");
        try {
            const data = await get("/api/payments");
            setPayments(data.payments || []);
            setView("history");
        } catch (e) {
            setMsg((e && e.text) || e.message || "Failed to load payments");
        }
    }

    // ===== Employee portal =====
    async function loadEmployeePayments(initialStatus = "PendingVerification") {
        setMsg("");
        try {
            const data = await get(`/api/employee/payments?status=${encodeURIComponent(initialStatus)}`);
            setEmpStatusFilter(initialStatus);
            setEmpPayments(data.payments || []);
            setView("emp");
        } catch (e) {
            const d = e && e.data;
            setMsg((d && d.error) || (e && e.text) || e.message || "Failed to load employee payments");
        }
    }

    async function verifyPayment(id) {
        setMsg("");
        try {
            await post(`/api/employee/payments/${id}/verify`, {});
            setMsg("Payment verified.");
            await loadEmployeePayments(empStatusFilter);
        } catch (e) {
            const d = e && e.data;
            if (d && d.error) {
                setMsg(d.error);
                return;
            }
            setMsg((e && e.text) || e.message || "Failed to verify payment");
        }
    }

    async function submitPayment(id) {
        setMsg("");
        try {
            await post(`/api/employee/payments/${id}/submit`, {});
            setMsg("Payment submitted to SWIFT.");
            await loadEmployeePayments(empStatusFilter);
        } catch (e) {
            const d = e && e.data;
            if (d && d.error) {
                setMsg(d.error);
                return;
            }
            setMsg((e && e.text) || e.message || "Failed to submit payment");
        }
    }

    // ===== Create Employee (admin/staff) =====
    async function doCreateEmployee() {
        setMsg("");
        const payload = {
            fullName: empFullName.value.trim(),
            idNumber: empIdNumber.value.trim(),
            accountNumber: empAccountNumber.value.trim(),
            username: empUsername.value.trim(),
            password: empPassword.value
        };

        const e = validateRegister(payload);
        setEmpRegErrs(e);
        if (Object.keys(e).length) {
            setMsg("Please fix the highlighted fields in the employee form.");
            return;
        }

        try {
            await post("/api/admin/employees", payload);
            setMsg("Employee created successfully.");

            // clear form + errors
            setEmpRegErrs({});
            empFullName.set("");
            empIdNumber.set("");
            empAccountNumber.set("");
            empUsername.set("");
            empPassword.set("");
        } catch (err) {
            const d = err && err.data;
            if (d && d.details) {
                const mapped = {
                    fullName: d.details.FullName && d.details.FullName[0],
                    idNumber: d.details.IdNumber && d.details.IdNumber[0],
                    accountNumber: d.details.AccountNumber && d.details.AccountNumber[0],
                    username: d.details.Username && d.details.Username[0],
                    password: d.details.Password && d.details.Password[0]
                };
                setEmpRegErrs(prev => ({ ...prev, ...mapped }));
                setMsg("Please fix the highlighted fields in the employee form.");
                return;
            }
            if (d && d.error) {
                setMsg(d.error);
                return;
            }
            setMsg(err.text || err.message || "Failed to create employee");
        }
    }

    return (
        <div className="app-shell">
            <div className="app-card">
                <div className="app-header">
                    <div>
                        <h1 className="app-title">Customer International Payments Portal</h1>
                        <p className="app-subtitle">
                            Secure international payments for customers, with staff verification.
                        </p>
                    </div>
                    {currentUser && (
                        <div className="app-user">
                            Logged in as <strong>{currentUser.username}</strong>
                            {isEmployee && " (Employee)"}
                        </div>
                    )}
                </div>

                <div className="toolbar">
                    <button
                        className="btn btn-secondary"
                        onClick={goRegister}
                    >
                        Register (Customer)
                    </button>
                    <button
                        className="btn btn-primary"
                        onClick={goLogin}
                    >
                        Login
                    </button>
                    <button
                        className="btn btn-secondary"
                        onClick={() => goProtected("pay")}
                        disabled={!isAuthed}
                    >
                        Make Payment
                    </button>
                    <button
                        className="btn btn-secondary"
                        onClick={loadPayments}
                        disabled={!isAuthed}
                    >
                        My Payments
                    </button>
                    {isEmployee && (
                        <button
                            className="btn btn-outline"
                            onClick={() => loadEmployeePayments("PendingVerification")}
                            disabled={!isAuthed}
                        >
                            Employee Payments
                        </button>
                    )}
                </div>

                {msg && (
                    <div className={"app-message" + (msg.indexOf("fix") !== -1 ? " error" : "")}>
                        {msg}
                    </div>
                )}

                {view === "register" && (
                    <section>
                        <h2>Register (Customer)</h2>
                        <Field
                            label="Full name"
                            value={full_name.value}
                            onChange={onChangeRegister.fullName}
                            hint="2-60 letters; may include spaces and , . ' -"
                            error={regErrs.fullName}
                        />
                        <Field
                            label="ID number"
                            value={id_number.value}
                            onChange={onChangeRegister.idNumber}
                            hint="6-20 characters; letters/digits/hyphen only"
                            error={regErrs.idNumber}
                        />
                        <Field
                            label="Account number"
                            value={account_number.value}
                            onChange={onChangeRegister.accountNumber}
                            hint="Digits only, 8-20 long"
                            error={regErrs.accountNumber}
                        />
                        <Field
                            label="Username"
                            value={username.value}
                            onChange={onChangeRegister.username}
                            hint="3-30; letters/digits/_ . -"
                            error={regErrs.username}
                        />
                        <Field
                            label="Password"
                            type="password"
                            value={password.value}
                            onChange={onChangeRegister.password}
                            hint={`${PASS_MIN}-${PASS_MAX} characters`}
                            error={regErrs.password}
                        />
                        <button className="btn btn-primary" onClick={doRegister}>
                            Create account
                        </button>
                    </section>
                )}

                {view === "login" && (
                    <section>
                        <h2>Login (Customer or Employee)</h2>
                        <Field
                            label="Username"
                            value={luser.value}
                            onChange={onChangeLogin.username}
                            hint="3-30; letters/digits/_ . -"
                            error={logErrs.username}
                        />
                        <Field
                            label="Account number"
                            value={lacc.value}
                            onChange={onChangeLogin.accountNumber}
                            hint="Digits only, 8-20 long"
                            error={logErrs.accountNumber}
                        />
                        <Field
                            label="Password"
                            type="password"
                            value={lpass.value}
                            onChange={onChangeLogin.password}
                            hint={`${PASS_MIN}-${PASS_MAX} characters`}
                            error={logErrs.password}
                        />
                        <button className="btn btn-primary" onClick={doLogin}>
                            Login
                        </button>
                    </section>
                )}

                {view === "pay" && (
                    <section>
                        <h2>Make international payment</h2>
                        <Field
                            label="Amount"
                            placeholder="123.45"
                            value={amount.value}
                            onChange={amount.onChange}
                        />
                        <label className="field-label">Currency</label>
                        <select
                            value={currency.value}
                            onChange={currency.onChange}
                            style={{
                                display: "block",
                                width: "100%",
                                padding: 8,
                                marginBottom: 8,
                                borderRadius: 6,
                                border: "1px solid #d1d5db"
                            }}
                        >
                            {["ZAR", "USD", "EUR", "GBP", "AUD", "CAD", "JPY", "CNY"].map(c => (
                                <option key={c} value={c}>
                                    {c}
                                </option>
                            ))}
                        </select>
                        <Field
                            label="Provider"
                            value={provider.value}
                            onChange={provider.onChange}
                        />
                        <Field
                            label="Payee Account"
                            value={payee_account.value}
                            onChange={payee_account.onChange}
                        />
                        <Field
                            label="SWIFT/BIC"
                            value={swift_bic.value}
                            onChange={swift_bic.onChange}
                        />
                        <button className="btn btn-primary" onClick={doPayment}>
                            Pay Now
                        </button>
                    </section>
                )}

                {view === "history" && (
                    <section>
                        <h2>My Payments</h2>
                        <ul className="payments-list">
                            {payments.map(p => (
                                <li key={p.id}>
                                    {p.currency} {(p.amountCents / 100).toFixed(2)} - {p.status} - {p.swiftBic}
                                </li>
                            ))}
                        </ul>
                    </section>
                )}

                {view === "emp" && isEmployee && (
                    <section>
                        <h2>Employee International Payments Portal</h2>

                        {/* Add Employee form */}
                        <div className="card" style={{ marginBottom: 16, padding: 12 }}>
                            <h3 style={{ marginTop: 0 }}>Add Employee</h3>
                            <Field
                                label="Full name"
                                value={empFullName.value}
                                onChange={e => {
                                    empFullName.onChange(e);
                                    setEmpRegErrs(prev => ({
                                        ...prev,
                                        fullName: rules.fullName(e.target.value)
                                    }));
                                }}
                                hint="2-60 letters; may include spaces and , . ' -"
                                error={empRegErrs.fullName}
                            />
                            <Field
                                label="ID number"
                                value={empIdNumber.value}
                                onChange={e => {
                                    empIdNumber.onChange(e);
                                    setEmpRegErrs(prev => ({
                                        ...prev,
                                        idNumber: rules.idNumber(e.target.value)
                                    }));
                                }}
                                hint="6-20 characters; letters/digits/hyphen only"
                                error={empRegErrs.idNumber}
                            />
                            <Field
                                label="Account number"
                                value={empAccountNumber.value}
                                onChange={e => {
                                    empAccountNumber.onChange(e);
                                    setEmpRegErrs(prev => ({
                                        ...prev,
                                        accountNumber: rules.accountNumber(e.target.value)
                                    }));
                                }}
                                hint="Digits only, 8-20 long"
                                error={empRegErrs.accountNumber}
                            />
                            <Field
                                label="Username"
                                value={empUsername.value}
                                onChange={e => {
                                    empUsername.onChange(e);
                                    setEmpRegErrs(prev => ({
                                        ...prev,
                                        username: rules.username(e.target.value)
                                    }));
                                }}
                                hint="3-30; letters/digits/_ . -"
                                error={empRegErrs.username}
                            />
                            <Field
                                label="Password"
                                type="password"
                                value={empPassword.value}
                                onChange={e => {
                                    empPassword.onChange(e);
                                    setEmpRegErrs(prev => ({
                                        ...prev,
                                        password: rules.password(e.target.value)
                                    }));
                                }}
                                hint={`${PASS_MIN}-${PASS_MAX} characters`}
                                error={empRegErrs.password}
                            />
                            <button className="btn btn-primary" onClick={doCreateEmployee}>
                                Create employee
                            </button>
                        </div>

                        <div style={{ marginBottom: 10 }}>
                            <label>Status filter: </label>
                            <select
                                value={empStatusFilter}
                                onChange={async e => {
                                    const val = e.target.value;
                                    setEmpStatusFilter(val);
                                    await loadEmployeePayments(val);
                                }}
                            >
                                <option value="PendingVerification">PendingVerification</option>
                                <option value="Verified">Verified</option>
                                <option value="SubmittedToSwift">SubmittedToSwift</option>
                            </select>
                        </div>

                        <table className="emp-table">
                            <thead>
                                <tr>
                                    <th>Created</th>
                                    <th>Customer</th>
                                    <th>Amount</th>
                                    <th>Currency</th>
                                    <th>Payee Account</th>
                                    <th>SWIFT/BIC</th>
                                    <th>Status</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {empPayments.map(p => (
                                    <tr key={p.id}>
                                        <td>{new Date(p.createdAt).toLocaleString()}</td>
                                        <td>{p.customerUsername}</td>
                                        <td>{(p.amountCents / 100).toFixed(2)}</td>
                                        <td>{p.currency}</td>
                                        <td>{p.payeeAccount}</td>
                                        <td>{p.swiftBic}</td>
                                        <td>{p.status}</td>
                                        <td>
                                            {!p.submittedToSwift && !p.isVerified && (
                                                <button
                                                    className="btn btn-outline"
                                                    onClick={() => verifyPayment(p.id)}
                                                    style={{ marginRight: 4 }}
                                                >
                                                    Verify
                                                </button>
                                            )}
                                            {!p.submittedToSwift && p.isVerified && (
                                                <button
                                                    className="btn btn-primary"
                                                    onClick={() => submitPayment(p.id)}
                                                >
                                                    Submit to SWIFT
                                                </button>
                                            )}
                                            {p.submittedToSwift && <span>Completed</span>}
                                        </td>
                                    </tr>
                                ))}
                                {empPayments.length === 0 && (
                                    <tr>
                                        <td colSpan={8}>No payments in this state.</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </section>
                )}
            </div>
        </div>
    );
}

// ==== Field component with hint + per-field error ====
function Field({ label, type = "text", value, onChange, placeholder, hint, error }) {
    return (
        <label className="field">
            <div className="field-label">{label}</div>
            <input
                type={type}
                value={value}
                onChange={onChange}
                placeholder={placeholder}
                aria-invalid={!!error}
                aria-describedby={hint ? `${label}-hint` : undefined}
                className={"field-input" + (error ? " error" : "")}
            />
            {hint && (
                <div
                    id={`${label}-hint`}
                    className="field-hint"
                >
                    {hint}
                </div>
            )}
            {error && (
                <div className="field-error">
                    {error}
                </div>
            )}
        </label>
    );
}
