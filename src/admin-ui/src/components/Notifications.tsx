import { observer } from "mobx-react-lite";
import { Snackbar, Alert } from "@mui/material";
import { notifyStore } from "@/stores/notify.store";

export const Notifications = observer(function Notifications() {
  return (
    <Snackbar
      open={notifyStore.open}
      autoHideDuration={4000}
      onClose={() => notifyStore.close()}
      anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
    >
      <Alert
        onClose={() => notifyStore.close()}
        severity={notifyStore.severity}
        variant="filled"
        sx={{ minWidth: 280, fontSize: "0.875rem" }}
      >
        {notifyStore.message}
      </Alert>
    </Snackbar>
  );
});
