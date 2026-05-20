-- WARNING: This schema is for context only and is not meant to be run.
-- Table order and constraints may not be valid for execution.

CREATE TABLE public.academic_years (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  label text NOT NULL UNIQUE,
  start_date date,
  end_date date,
  is_active boolean NOT NULL DEFAULT false,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT academic_years_pkey PRIMARY KEY (id)
);
CREATE TABLE public.attempt_answer_options (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  attempt_answer_id uuid NOT NULL,
  snapshot_option_id uuid NOT NULL,
  CONSTRAINT attempt_answer_options_pkey PRIMARY KEY (id),
  CONSTRAINT attempt_answer_options_attempt_answer_id_fkey FOREIGN KEY (attempt_answer_id) REFERENCES public.attempt_answers(id),
  CONSTRAINT attempt_answer_options_snapshot_option_id_fkey FOREIGN KEY (snapshot_option_id) REFERENCES public.snapshot_options(id)
);
CREATE TABLE public.attempt_answers (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  attempt_id uuid NOT NULL,
  snapshot_question_id uuid NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT attempt_answers_pkey PRIMARY KEY (id),
  CONSTRAINT attempt_answers_attempt_id_fkey FOREIGN KEY (attempt_id) REFERENCES public.attempts(id),
  CONSTRAINT attempt_answers_snapshot_question_id_fkey FOREIGN KEY (snapshot_question_id) REFERENCES public.snapshot_questions(id)
);
CREATE TABLE public.attempts (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  quiz_id uuid NOT NULL,
  snapshot_id uuid,
  started_at timestamp with time zone,
  submitted_at timestamp with time zone,
  score numeric,
  duration_seconds integer,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT attempts_pkey PRIMARY KEY (id),
  CONSTRAINT attempts_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id),
  CONSTRAINT attempts_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id),
  CONSTRAINT attempts_snapshot_id_fkey FOREIGN KEY (snapshot_id) REFERENCES public.quiz_snapshots(id)
);
CREATE TABLE public.classes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  name text NOT NULL,
  course_id uuid,
  academic_year_id uuid,
  semester smallint,
  lecturer_id uuid,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT classes_pkey PRIMARY KEY (id),
  CONSTRAINT classes_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id),
  CONSTRAINT classes_academic_year_id_fkey FOREIGN KEY (academic_year_id) REFERENCES public.academic_years(id),
  CONSTRAINT classes_lecturer_id_fkey FOREIGN KEY (lecturer_id) REFERENCES public.lecturers(id)
);
CREATE TABLE public.courses (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  code text UNIQUE,
  name text NOT NULL,
  major_id uuid,
  credits integer,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT courses_pkey PRIMARY KEY (id),
  CONSTRAINT courses_major_id_fkey FOREIGN KEY (major_id) REFERENCES public.majors(id)
);
CREATE TABLE public.folders (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  name text NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT folders_pkey PRIMARY KEY (id),
  CONSTRAINT folders_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id)
);
CREATE TABLE public.lecturers (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  staff_number text UNIQUE,
  full_name text NOT NULL,
  email text UNIQUE,
  major_id uuid,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT lecturers_pkey PRIMARY KEY (id),
  CONSTRAINT lecturers_major_id_fkey FOREIGN KEY (major_id) REFERENCES public.majors(id)
);
CREATE TABLE public.majors (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  code text UNIQUE,
  name text NOT NULL UNIQUE,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT majors_pkey PRIMARY KEY (id)
);
CREATE TABLE public.options (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  question_id uuid NOT NULL,
  content text NOT NULL,
  is_correct boolean NOT NULL DEFAULT false,
  order_index integer NOT NULL DEFAULT 0,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  deleted_at timestamp with time zone,
  CONSTRAINT options_pkey PRIMARY KEY (id),
  CONSTRAINT options_question_id_fkey FOREIGN KEY (question_id) REFERENCES public.questions(id)
);
CREATE TABLE public.profiles (
  id uuid NOT NULL,
  first_name text,
  last_name text,
  username text UNIQUE,
  major_id uuid,
  year_of_entry integer,
  role text NOT NULL DEFAULT 'student'::text,
  avatar_notion_page_id text,
  avatar_url text,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  deleted_at timestamp with time zone,
  CONSTRAINT profiles_pkey PRIMARY KEY (id),
  CONSTRAINT profiles_id_fkey FOREIGN KEY (id) REFERENCES auth.users(id),
  CONSTRAINT profiles_major_id_fkey FOREIGN KEY (major_id) REFERENCES public.majors(id)
);
CREATE TABLE public.questions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  quiz_id uuid NOT NULL,
  content text NOT NULL,
  question_type text NOT NULL CHECK (question_type = ANY (ARRAY['multiple_choice'::text, 'checkbox'::text])),
  order_index integer NOT NULL DEFAULT 0,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  deleted_at timestamp with time zone,
  image_url text,
  image_notion_page_id text,
  CONSTRAINT questions_pkey PRIMARY KEY (id),
  CONSTRAINT questions_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id)
);
CREATE TABLE public.quiz_copies (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  original_quiz_id uuid NOT NULL,
  new_quiz_id uuid NOT NULL,
  copied_by_user_id uuid,
  copied_at timestamp with time zone DEFAULT now(),
  CONSTRAINT quiz_copies_pkey PRIMARY KEY (id),
  CONSTRAINT quiz_copies_original_quiz_id_fkey FOREIGN KEY (original_quiz_id) REFERENCES public.quizzes(id),
  CONSTRAINT quiz_copies_new_quiz_id_fkey FOREIGN KEY (new_quiz_id) REFERENCES public.quizzes(id),
  CONSTRAINT quiz_copies_copied_by_user_id_fkey FOREIGN KEY (copied_by_user_id) REFERENCES public.profiles(id)
);
CREATE TABLE public.quiz_histories (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  quiz_id uuid NOT NULL,
  last_opened_at timestamp with time zone NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT quiz_histories_pkey PRIMARY KEY (id),
  CONSTRAINT quiz_histories_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id),
  CONSTRAINT quiz_histories_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id)
);
CREATE TABLE public.quiz_snapshots (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  quiz_id uuid NOT NULL,
  version_number integer NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT quiz_snapshots_pkey PRIMARY KEY (id),
  CONSTRAINT quiz_snapshots_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id)
);
CREATE TABLE public.quiz_tags (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  quiz_id uuid NOT NULL,
  tag_id uuid NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT quiz_tags_pkey PRIMARY KEY (id),
  CONSTRAINT quiz_tags_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id),
  CONSTRAINT quiz_tags_tag_id_fkey FOREIGN KEY (tag_id) REFERENCES public.tags(id)
);
CREATE TABLE public.quiz_topics (
  quiz_id uuid NOT NULL,
  topic_id uuid NOT NULL,
  is_primary boolean NOT NULL DEFAULT false,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT quiz_topics_pkey PRIMARY KEY (quiz_id, topic_id),
  CONSTRAINT quiz_topics_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id),
  CONSTRAINT quiz_topics_topic_id_fkey FOREIGN KEY (topic_id) REFERENCES public.topics(id)
);
CREATE TABLE public.quizzes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  author_id uuid NOT NULL,
  title text NOT NULL,
  description text,
  time_limit_minutes integer,
  major_id uuid,
  course_id uuid,
  lecturer_id uuid,
  folder_id uuid,
  visibility text NOT NULL DEFAULT 'draft'::text CHECK (visibility = ANY (ARRAY['draft'::text, 'published'::text, 'archived'::text])),
  access text NOT NULL DEFAULT 'private'::text CHECK (access = ANY (ARRAY['private'::text, 'public'::text])),
  allow_copy boolean NOT NULL DEFAULT false,
  version_number integer NOT NULL DEFAULT 1,
  has_been_updated boolean NOT NULL DEFAULT false,
  cover_image_notion_page_id text,
  cover_image_url text,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  deleted_at timestamp with time zone,
  slug text,
  CONSTRAINT quizzes_pkey PRIMARY KEY (id),
  CONSTRAINT quizzes_author_id_fkey FOREIGN KEY (author_id) REFERENCES public.profiles(id),
  CONSTRAINT quizzes_major_id_fkey FOREIGN KEY (major_id) REFERENCES public.majors(id),
  CONSTRAINT quizzes_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id),
  CONSTRAINT quizzes_lecturer_id_fkey FOREIGN KEY (lecturer_id) REFERENCES public.lecturers(id),
  CONSTRAINT quizzes_folder_id_fkey FOREIGN KEY (folder_id) REFERENCES public.folders(id)
);
CREATE TABLE public.snapshot_options (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  snapshot_question_id uuid NOT NULL,
  original_option_id uuid,
  content text NOT NULL,
  is_correct boolean NOT NULL DEFAULT false,
  order_index integer NOT NULL,
  CONSTRAINT snapshot_options_pkey PRIMARY KEY (id),
  CONSTRAINT snapshot_options_snapshot_question_id_fkey FOREIGN KEY (snapshot_question_id) REFERENCES public.snapshot_questions(id),
  CONSTRAINT snapshot_options_original_option_id_fkey FOREIGN KEY (original_option_id) REFERENCES public.options(id)
);
CREATE TABLE public.snapshot_questions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  snapshot_id uuid NOT NULL,
  original_question_id uuid,
  content text NOT NULL,
  question_type text NOT NULL,
  order_index integer NOT NULL,
  explanation text,
  CONSTRAINT snapshot_questions_pkey PRIMARY KEY (id),
  CONSTRAINT snapshot_questions_snapshot_id_fkey FOREIGN KEY (snapshot_id) REFERENCES public.quiz_snapshots(id),
  CONSTRAINT snapshot_questions_original_question_id_fkey FOREIGN KEY (original_question_id) REFERENCES public.questions(id)
);
CREATE TABLE public.tags (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  name text NOT NULL UNIQUE,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  usage_count integer NOT NULL DEFAULT 0,
  CONSTRAINT tags_pkey PRIMARY KEY (id)
);
CREATE TABLE public.topics (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  name text NOT NULL UNIQUE,
  description text,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT topics_pkey PRIMARY KEY (id)
);
CREATE TABLE public.user_quiz_stats (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  quiz_id uuid NOT NULL,
  attempt_count integer NOT NULL DEFAULT 0,
  last_score numeric,
  avg_score numeric,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT user_quiz_stats_pkey PRIMARY KEY (id),
  CONSTRAINT user_quiz_stats_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id),
  CONSTRAINT user_quiz_stats_quiz_id_fkey FOREIGN KEY (quiz_id) REFERENCES public.quizzes(id)
);