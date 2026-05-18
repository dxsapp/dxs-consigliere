import {
  Box,
  Chip,
  Divider,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { NavLink } from "react-router-dom";
import {
  OPERATOR_ROUTES,
  SYSTEM_ROUTES,
  type NavRoute,
} from "@/app/routes";

/**
 * Sidebar nav (per design brief §4):
 *   Operator section  — 6 default routes
 *   System  section   — 8 advanced routes, visually separated,
 *                       each with a "DEV" chip
 *
 * Behaviour:
 *  - desktop (md and up): `permanent` Drawer.
 *  - mobile (xs+sm):       `temporary` Drawer toggled from the
 *                          AppBar's menu button.
 */
export function AppDrawer({
  mobileOpen,
  onClose,
}: {
  mobileOpen: boolean;
  onClose: () => void;
}) {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const width = theme.layout.drawerWidth;

  const content = (
    <Box sx={{ width, display: "flex", flexDirection: "column", height: "100%" }}>
      <Toolbar sx={{ gap: 1.5 }}>
        <Box
          sx={{
            width: 32,
            height: 32,
            borderRadius: 1,
            background: `linear-gradient(135deg, ${theme.palette.primary.main}, ${theme.palette.secondary.main})`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <span className="material-symbols-outlined" style={{ fontSize: 20, color: theme.palette.background.paper }}>
            memory
          </span>
        </Box>
        <Stack>
          <Typography variant="subtitle2">Consigliere</Typography>
          <Typography variant="caption" color="text.secondary">
            admin
          </Typography>
        </Stack>
      </Toolbar>

      <SectionLabel label="Operator" />
      <NavList routes={OPERATOR_ROUTES} onItemClick={!isDesktop ? onClose : undefined} />

      <Divider sx={{ my: 1, mx: 2 }} />

      <SectionLabel label="System" devChip />
      <NavList routes={SYSTEM_ROUTES} devChip onItemClick={!isDesktop ? onClose : undefined} />
    </Box>
  );

  if (isDesktop) {
    return (
      <Drawer
        variant="permanent"
        sx={{
          width,
          flexShrink: 0,
          "& .MuiDrawer-paper": { width, boxSizing: "border-box" },
        }}
      >
        {content}
      </Drawer>
    );
  }

  return (
    <Drawer
      variant="temporary"
      open={mobileOpen}
      onClose={onClose}
      ModalProps={{ keepMounted: true }}
      sx={{ "& .MuiDrawer-paper": { width, boxSizing: "border-box" } }}
    >
      {content}
    </Drawer>
  );
}

function SectionLabel({ label, devChip }: { label: string; devChip?: boolean }) {
  return (
    <Stack direction="row" alignItems="center" spacing={1} sx={{ px: 2, pt: 1.5, pb: 0.5 }}>
      <Typography variant="overline" color="text.secondary">
        {label}
      </Typography>
      {devChip && <Chip label="DEV" size="small" color="warning" variant="outlined" />}
    </Stack>
  );
}

function NavList({
  routes,
  devChip,
  onItemClick,
}: {
  routes: NavRoute[];
  devChip?: boolean;
  onItemClick?: () => void;
}) {
  return (
    <List dense>
      {routes.map((r) => (
        <ListItemButton
          key={r.id}
          component={NavLink}
          to={r.path}
          onClick={onItemClick}
          sx={{
            mx: 1,
            borderRadius: 1,
            "&.active": (theme) => ({
              backgroundColor: theme.palette.action.selected,
              color: theme.palette.primary.main,
              "& .MuiListItemIcon-root": { color: theme.palette.primary.main },
            }),
          }}
        >
          <ListItemIcon sx={{ minWidth: 36 }}>
            <span className="material-symbols-outlined" style={{ fontSize: 20 }}>
              {r.icon}
            </span>
          </ListItemIcon>
          <ListItemText
            primary={r.label}
            primaryTypographyProps={{ variant: "body2" }}
            secondary={devChip ? "advanced" : undefined}
            secondaryTypographyProps={{ variant: "caption" }}
          />
        </ListItemButton>
      ))}
    </List>
  );
}
