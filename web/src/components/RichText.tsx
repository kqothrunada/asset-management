import { useEffect } from "react";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";

type Props = { value: string; onChange: (html: string) => void; disabled?: boolean };

export default function RichText({ value, onChange, disabled }: Props) {
    const editor = useEditor({
        extensions: [StarterKit],
        content: value,
        editable: !disabled,
        onUpdate: ({ editor }) => onChange(editor.isEmpty ? "" : editor.getHTML()),
    });

    useEffect(() => {
        if (editor && value !== editor.getHTML() && !(editor.isEmpty && !value)) editor.commands.setContent(value || "", false);
    }, [value, editor]);
    useEffect(() => { editor?.setEditable(!disabled); }, [disabled, editor]);

    if (!editor) return null;
    const btn = (label: string, on: () => void, active: boolean) => (
        <button type="button" className={active ? "active" : ""} onClick={on} disabled={disabled}>{label}</button>
    );
    return (
        <div className={`rte ${disabled ? "disabled" : ""}`}>
            <div className="rte-bar">
                {btn("B", () => editor.chain().focus().toggleBold().run(), editor.isActive("bold"))}
                {btn("I", () => editor.chain().focus().toggleItalic().run(), editor.isActive("italic"))}
                {btn("H2", () => editor.chain().focus().toggleHeading({ level: 2 }).run(), editor.isActive("heading", { level: 2 }))}
                {btn("• List", () => editor.chain().focus().toggleBulletList().run(), editor.isActive("bulletList"))}
                {btn("1. List", () => editor.chain().focus().toggleOrderedList().run(), editor.isActive("orderedList"))}
            </div>
            <EditorContent editor={editor} />
        </div>
    );
}