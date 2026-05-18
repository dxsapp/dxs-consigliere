import {
  Step,
  StepConnector,
  stepConnectorClasses,
  StepLabel,
  Stepper,
  Typography,
  styled,
  useTheme,
  type Theme,
} from "@mui/material";
import CancelIcon from "@mui/icons-material/Cancel";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import RadioButtonCheckedIcon from "@mui/icons-material/RadioButtonChecked";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import type { ReactNode } from "react";

/**
 * S5 — shared vertical timeline used by every entity-detail screen
 * (Transaction, Address, Token). Each stage has a status that maps
 * to severity colours from the theme palette. The stage `detail`
 * slot accepts arbitrary content rendered under the step label
 * (e.g. payload chips, error reason, sub-counts).
 *
 * Status semantics:
 *  - pending  — not yet reached (greyed out, hollow circle)
 *  - active   — currently in flight (primary colour, filled dot)
 *  - done     — completed (success colour, checkmark)
 *  - failed   — terminal failure (error colour, X)
 *  - warning  — soft anomaly (warning colour, !)
 *  - unknown  — no observation yet for a non-pending step
 */
export type EntityTimelineStageStatus =
  | "pending"
  | "active"
  | "done"
  | "failed"
  | "warning"
  | "unknown";

export interface EntityTimelineStage {
  /** Stable key for React reconciliation + tests. */
  key: string;
  /** Display label. */
  label: string;
  /** Lifecycle status — drives colour + icon. */
  status: EntityTimelineStageStatus;
  /** Optional unix-ms timestamp shown as a subtitle. */
  timestampMs?: number | null;
  /** Optional rich detail rendered under the step label. */
  detail?: ReactNode;
}

/**
 * Vertical connector that mirrors the active stage's colour, so the
 * eye can follow a single thread down the timeline. Uses the same
 * severity tokens declared in theme augmentation.
 */
const ColoredConnector = styled(StepConnector)(({ theme }) => ({
  [`& .${stepConnectorClasses.line}`]: {
    borderColor: theme.palette.divider,
    minHeight: 24,
  },
  [`&.${stepConnectorClasses.active} .${stepConnectorClasses.line}`]: {
    borderColor: theme.palette.primary.main,
  },
  [`&.${stepConnectorClasses.completed} .${stepConnectorClasses.line}`]: {
    borderColor: theme.palette.severity.success.main,
  },
}));

export function EntityTimeline({
  stages,
  ariaLabel,
}: {
  stages: EntityTimelineStage[];
  ariaLabel?: string;
}) {
  const theme = useTheme();
  // The Stepper's `activeStep` index is the first non-done stage.
  // Failures short-circuit (we still highlight up to the failed step).
  const firstActiveIdx = stages.findIndex((s) => s.status === "active");
  const failedIdx = stages.findIndex((s) => s.status === "failed");
  const activeStep =
    failedIdx >= 0 ? failedIdx : firstActiveIdx >= 0 ? firstActiveIdx : stages.length;

  return (
    <Stepper
      activeStep={activeStep}
      orientation="vertical"
      connector={<ColoredConnector />}
      aria-label={ariaLabel}
      data-testid="entity-timeline"
    >
      {stages.map((s) => (
        <Step
          key={s.key}
          active={s.status === "active"}
          completed={s.status === "done"}
          data-testid={`timeline-stage-${s.key}`}
          data-status={s.status}
        >
          <StepLabel
            slots={{ stepIcon: () => renderIcon(s.status, theme.palette) }}
            optional={
              s.timestampMs ? (
                <Typography variant="caption" color="text.secondary">
                  {new Date(s.timestampMs).toUTCString().slice(5, 25)} UTC
                </Typography>
              ) : null
            }
          >
            <Typography variant="body1" component="div">
              {s.label}
            </Typography>
            {s.detail && (
              <Typography
                component="div"
                variant="body2"
                color="text.secondary"
                sx={{ mt: 0.5 }}
              >
                {s.detail}
              </Typography>
            )}
          </StepLabel>
        </Step>
      ))}
    </Stepper>
  );
}

function renderIcon(status: EntityTimelineStageStatus, palette: Theme["palette"]) {
  switch (status) {
    case "done":
      return <CheckCircleIcon sx={{ color: palette.severity.success.main }} />;
    case "active":
      return <RadioButtonCheckedIcon sx={{ color: palette.primary.main }} />;
    case "failed":
      return <CancelIcon sx={{ color: palette.severity.error.main }} />;
    case "warning":
      return <WarningAmberIcon sx={{ color: palette.severity.warning.main }} />;
    case "unknown":
      return <HelpOutlineIcon sx={{ color: palette.text.disabled }} />;
    case "pending":
    default:
      return <RadioButtonUncheckedIcon sx={{ color: palette.text.disabled }} />;
  }
}
