import { Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { AuthService } from "../../core/auth.service";

@Component({
  selector: "app-login",
  imports: [ReactiveFormsModule],
  templateUrl: "./login.component.html",
  styleUrl: "./login.component.css",
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  loading = false;
  error = "";

  form = this.fb.nonNullable.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.loading = true;
    this.error = "";

    this.auth
      .login(this.form.controls.email.value, this.form.controls.password.value)
      .subscribe({
        next: () => {
          this.loading = false;
          void this.router.navigate(["/"]);
        },
        error: () => {
          this.loading = false;
          this.error = "Invalid email or password.";
        },
      });
  }
}
