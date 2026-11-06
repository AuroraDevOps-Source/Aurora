BEGIN;
ALTER TABLE "AspNetUsers" ADD COLUMN "InvitationPending" boolean NOT NULL DEFAULT false;
COMMIT;
