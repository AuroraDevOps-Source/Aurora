BEGIN;
ALTER TABLE public.update_notice ADD COLUMN message_template text
    CHECK (message_template IS NULL OR char_length(message_template) BETWEEN 1 AND 1000);
GRANT UPDATE(message_template) ON public.update_notice TO aurora_app;
COMMIT;
